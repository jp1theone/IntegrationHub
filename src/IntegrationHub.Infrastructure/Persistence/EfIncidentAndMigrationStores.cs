using IntegrationHub.Application.Common;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Application.Migrations;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Migrations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfIncidentStore(IntegrationHubDbContext db) : IIncidentStore
{
    public async Task<PagedResult<IncidentDto>> SearchAsync(string? query, IncidentStatus? status, IncidentSeverity? severity, int page, int pageSize, CancellationToken ct)
    {
        var incidents = db.Incidents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) incidents = incidents.Where(x => EF.Functions.Like(x.IncidentNumber, $"%{query}%") || EF.Functions.Like(x.Title, $"%{query}%") || (x.ErrorCode != null && EF.Functions.Like(x.ErrorCode, $"%{query}%")));
        if (status.HasValue) incidents = incidents.Where(x => x.Status == status.Value);
        if (severity.HasValue) incidents = incidents.Where(x => x.Severity == severity.Value);
        var total = await incidents.CountAsync(ct);
        var rows = await incidents.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => Map(x)).ToListAsync(ct);
        return new PagedResult<IncidentDto>(rows, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
    public Task<IncidentDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.Incidents.AsNoTracking().Where(x => x.Id == id).Select(x => Map(x)).SingleOrDefaultAsync(ct);
    public Task<Incident?> GetAsync(Guid id, CancellationToken ct) => db.Incidents.Include(x => x.Comments).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> IntegrationExistsAsync(Guid id, CancellationToken ct) => db.Integrations.AnyAsync(x => x.Id == id, ct);
    public async Task AddAsync(Incident incident, CancellationToken ct) { db.Incidents.Add(incident); await db.SaveChangesAsync(ct); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static IncidentDto Map(Incident x) => new(x.Id, x.IncidentNumber, x.IntegrationId, x.ExecutionId, x.Title, x.Description, x.Severity, x.Status, x.ErrorCode, x.AssignedToUserId, x.CreatedAt, x.UpdatedAt, x.ResolvedAt, x.Comments.OrderBy(c => c.CreatedAt).Select(c => new IncidentCommentDto(c.Id, c.AuthorUserId, c.Content, c.CreatedAt)).ToList());
}

public sealed class EfMigrationStore(IntegrationHubDbContext db) : IMigrationStore
{
    public async Task<PagedResult<MigrationDto>> SearchAsync(string? query, MigrationStatus? status, Guid? clientId, int page, int pageSize, CancellationToken ct)
    {
        var migrations = db.IntegrationMigrations.AsNoTracking().Include(x => x.History).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) migrations = migrations.Where(x => EF.Functions.Like(x.MigrationNumber, $"%{query}%") || EF.Functions.Like(x.SourceProvider, $"%{query}%") || EF.Functions.Like(x.TargetProvider, $"%{query}%"));
        if (status.HasValue) migrations = migrations.Where(x => x.Status == status.Value);
        if (clientId.HasValue) migrations = migrations.Where(x => x.ClientId == clientId.Value);
        var total = await migrations.CountAsync(ct);
        var rows = await migrations.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => Map(x)).ToListAsync(ct);
        return new PagedResult<MigrationDto>(rows, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
    public Task<MigrationDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.IntegrationMigrations.AsNoTracking().Where(x => x.Id == id).Select(x => Map(x)).SingleOrDefaultAsync(ct);
    public Task<Domain.Migrations.IntegrationMigration?> GetAsync(Guid id, CancellationToken ct) => db.IntegrationMigrations.Include(x => x.History).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> IntegrationBelongsToClientAsync(Guid integrationId, Guid clientId, CancellationToken ct) => db.Integrations.AnyAsync(x => x.Id == integrationId && x.ClientId == clientId, ct);
    public Task<bool> NumberExistsAsync(string number, CancellationToken ct) => db.IntegrationMigrations.AnyAsync(x => x.MigrationNumber == number, ct);
    public async Task AddAsync(Domain.Migrations.IntegrationMigration migration, CancellationToken ct) { db.IntegrationMigrations.Add(migration); await db.SaveChangesAsync(ct); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static MigrationDto Map(Domain.Migrations.IntegrationMigration x) => new(x.Id, x.MigrationNumber, x.ClientId, x.IntegrationId, x.SourceProvider, x.TargetProvider, x.Status, x.StartDate, x.EndDate, x.AssignedToUserId, x.Notes, x.CreatedAt, x.History.OrderBy(h => h.ChangedAt).Select(h => new MigrationHistoryDto(h.Status, h.ChangedByUserId, h.ChangedAt)).ToList());
}
