using IntegrationHub.Application.Clients;
using IntegrationHub.Application.Common;
using IntegrationHub.Application.Integrations;
using IntegrationHub.Application.Providers;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfClientStore(IntegrationHubDbContext db) : IClientStore
{
    public async Task<PagedResult<ClientDto>> SearchAsync(string? query, RecordStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var clients = db.Clients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) clients = clients.Where(x => EF.Functions.Like(x.Name, $"%{query}%") || EF.Functions.Like(x.Code, $"%{query}%") || EF.Functions.Like(x.Email, $"%{query}%"));
        if (status.HasValue) clients = clients.Where(x => x.Status == status.Value);
        var total = await clients.CountAsync(ct);
        var items = await clients.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClientDto(x.Id, x.Code, x.Name, x.LegalName, x.Email, x.Phone, x.Status, x.CreatedAt, x.Integrations.Count)).ToListAsync(ct);
        return new PagedResult<ClientDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
    public Task<Client?> GetAsync(Guid id, CancellationToken ct) => db.Clients.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ClientDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.Clients.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new ClientDto(x.Id, x.Code, x.Name, x.LegalName, x.Email, x.Phone, x.Status, x.CreatedAt, x.Integrations.Count)).SingleOrDefaultAsync(ct);
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) => db.Clients.AnyAsync(x => x.Code == code && x.Id != exceptId, ct);
    public async Task AddAsync(Client client, CancellationToken ct) { db.Clients.Add(client); await db.SaveChangesAsync(ct); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class EfProviderStore(IntegrationHubDbContext db) : IProviderStore
{
    public async Task<PagedResult<ProviderDto>> SearchAsync(string? query, RecordStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var providers = db.Providers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) providers = providers.Where(x => EF.Functions.Like(x.Name, $"%{query}%") || EF.Functions.Like(x.Code, $"%{query}%"));
        if (status.HasValue) providers = providers.Where(x => x.Status == status.Value);
        var total = await providers.CountAsync(ct);
        var items = await providers.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProviderDto(x.Id, x.Code, x.Name, x.Description, x.Type, x.Status, x.TimeoutSeconds, x.FailureMode, x.BaseUrl)).ToListAsync(ct);
        return new PagedResult<ProviderDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
    public Task<Provider?> GetAsync(Guid id, CancellationToken ct) => db.Providers.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProviderDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.Providers.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new ProviderDto(x.Id, x.Code, x.Name, x.Description, x.Type, x.Status, x.TimeoutSeconds, x.FailureMode, x.BaseUrl)).SingleOrDefaultAsync(ct);
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) => db.Providers.AnyAsync(x => x.Code == code && x.Id != exceptId, ct);
    public async Task AddAsync(Provider provider, CancellationToken ct) { db.Providers.Add(provider); await db.SaveChangesAsync(ct); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class EfIntegrationStore(IntegrationHubDbContext db) : IIntegrationStore
{
    public async Task<PagedResult<IntegrationDto>> SearchAsync(string? query, Guid? clientId, Guid? providerId, IntegrationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var integrations = db.Integrations.AsNoTracking().Include(x => x.Client).Include(x => x.Provider).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) integrations = integrations.Where(x => EF.Functions.Like(x.Name, $"%{query}%") || EF.Functions.Like(x.Code, $"%{query}%"));
        if (clientId.HasValue) integrations = integrations.Where(x => x.ClientId == clientId.Value);
        if (providerId.HasValue) integrations = integrations.Where(x => x.ProviderId == providerId.Value);
        if (status.HasValue) integrations = integrations.Where(x => x.Status == status.Value);
        var total = await integrations.CountAsync(ct);
        var items = await integrations.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new IntegrationDto(x.Id, x.Code, x.Name, x.Description, x.ClientId, x.Client!.Name, x.ProviderId, x.Provider!.Name, x.Type, x.Status, x.Environment, x.CreatedAt)).ToListAsync(ct);
        return new PagedResult<IntegrationDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
    public Task<Integration?> GetAsync(Guid id, CancellationToken ct) => db.Integrations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<IntegrationDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.Integrations.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new IntegrationDto(x.Id, x.Code, x.Name, x.Description, x.ClientId, x.Client!.Name, x.ProviderId, x.Provider!.Name, x.Type, x.Status, x.Environment, x.CreatedAt)).SingleOrDefaultAsync(ct);
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct) => db.Integrations.AnyAsync(x => x.Code == code && x.Id != exceptId, ct);
    public Task<bool> ClientExistsAsync(Guid id, CancellationToken ct) => db.Clients.AnyAsync(x => x.Id == id, ct);
    public Task<bool> ProviderExistsAsync(Guid id, CancellationToken ct) => db.Providers.AnyAsync(x => x.Id == id, ct);
    public async Task AddAsync(Integration integration, CancellationToken ct) { db.Integrations.Add(integration); await db.SaveChangesAsync(ct); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
