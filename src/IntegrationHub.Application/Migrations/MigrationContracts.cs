using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Migrations;

namespace IntegrationHub.Application.Migrations;

public sealed record MigrationDto(Guid Id, string MigrationNumber, Guid ClientId, Guid IntegrationId, string SourceProvider, string TargetProvider, MigrationStatus Status, DateTimeOffset? StartDate, DateTimeOffset? EndDate, string? AssignedToUserId, string? Notes, DateTimeOffset CreatedAt, IReadOnlyList<MigrationHistoryDto> History);
public sealed record MigrationHistoryDto(MigrationStatus Status, string? ChangedByUserId, DateTimeOffset ChangedAt);
public sealed record CreateMigrationRequest(Guid ClientId, Guid IntegrationId, string SourceProvider, string TargetProvider, string? AssignedToUserId, string? Notes);
public sealed record UpdateMigrationRequest(string? AssignedToUserId, string? Notes);

public interface IMigrationStore
{
    Task<PagedResult<MigrationDto>> SearchAsync(string? query, MigrationStatus? status, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken);
    Task<MigrationDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<IntegrationMigration?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IntegrationBelongsToClientAsync(Guid integrationId, Guid clientId, CancellationToken cancellationToken);
    Task<bool> NumberExistsAsync(string migrationNumber, CancellationToken cancellationToken);
    Task AddAsync(IntegrationMigration migration, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class MigrationUseCases(IMigrationStore migrations)
{
    public Task<PagedResult<MigrationDto>> SearchAsync(string? q, MigrationStatus? status, Guid? clientId, int page, int size, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, size);
        return migrations.SearchAsync(q?.Trim(), status, clientId, normalized.Page, normalized.PageSize, ct);
    }
    public Task<MigrationDto?> GetAsync(Guid id, CancellationToken ct) => migrations.GetDtoAsync(id, ct);
    public async Task<MigrationDto> CreateAsync(CreateMigrationRequest request, CancellationToken ct)
    {
        if (!await migrations.IntegrationBelongsToClientAsync(request.IntegrationId, request.ClientId, ct))
            throw new BusinessConflictException("The integration must belong to the selected client.");
        var number = $"MIG-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26];
        while (await migrations.NumberExistsAsync(number, ct)) number = $"MIG-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..26];
        var migration = new IntegrationMigration(number, request.ClientId, request.IntegrationId, request.SourceProvider.Trim(), request.TargetProvider.Trim(), request.AssignedToUserId, request.Notes?.Trim());
        migration.ChangeStatus(MigrationStatus.Planned, request.AssignedToUserId);
        await migrations.AddAsync(migration, ct);
        return (await migrations.GetDtoAsync(migration.Id, ct))!;
    }
    public async Task<bool> UpdateAsync(Guid id, UpdateMigrationRequest request, CancellationToken ct)
    {
        var migration = await migrations.GetAsync(id, ct); if (migration is null) return false;
        migration.Assign(request.AssignedToUserId); migration.AddNotes(request.Notes); await migrations.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> ChangeStatusAsync(Guid id, MigrationStatus status, string? actor, CancellationToken ct)
    {
        var migration = await migrations.GetAsync(id, ct); if (migration is null) return false;
        migration.ChangeStatus(status, actor); await migrations.SaveChangesAsync(ct); return true;
    }
}
