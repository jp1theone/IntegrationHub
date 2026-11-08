using IntegrationHub.Application.Common;
using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Providers;

namespace IntegrationHub.Application.Providers;

public sealed record ProviderDto(Guid Id, string Code, string Name, string Description, ProviderType Type, RecordStatus Status, int TimeoutSeconds, ProviderFailureMode FailureMode, string? BaseUrl);
public sealed record CreateProviderRequest(string Code, string Name, string Description, ProviderType Type, int TimeoutSeconds, string? BaseUrl = null);
public sealed record UpdateProviderRequest(string Name, string Description, int TimeoutSeconds, ProviderFailureMode FailureMode, string? BaseUrl = null);

public interface IProviderStore
{
    Task<PagedResult<ProviderDto>> SearchAsync(string? query, RecordStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Provider?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProviderDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(Provider provider, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ProviderUseCases(IProviderStore providers, IIntegrationProvider adapter)
{
    public Task<PagedResult<ProviderDto>> SearchAsync(string? q, RecordStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, pageSize);
        return providers.SearchAsync(q?.Trim(), status, normalized.Page, normalized.PageSize, ct);
    }
    public Task<ProviderDto?> GetAsync(Guid id, CancellationToken ct) => providers.GetDtoAsync(id, ct);
    public async Task<ProviderDto> CreateAsync(CreateProviderRequest request, CancellationToken ct)
    {
        if (await providers.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), null, ct)) throw new BusinessConflictException("A provider with this code already exists.");
        var entity = new Provider(request.Code, request.Name, request.Description, request.Type, request.TimeoutSeconds, request.BaseUrl);
        await providers.AddAsync(entity, ct);
        return (await providers.GetDtoAsync(entity.Id, ct))!;
    }
    public async Task<bool> UpdateAsync(Guid id, UpdateProviderRequest request, CancellationToken ct)
    {
        var entity = await providers.GetAsync(id, ct);
        if (entity is null) return false;
        entity.Configure(request.Name, request.Description, request.TimeoutSeconds, request.FailureMode, request.BaseUrl);
        await providers.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var entity = await providers.GetAsync(id, ct); if (entity is null) return false;
        entity.SetActive(active); await providers.SaveChangesAsync(ct); return true;
    }
    public async Task<ProviderHealthResult?> HealthAsync(Guid id, CancellationToken ct)
    {
        var entity = await providers.GetAsync(id, ct);
        return entity is null ? null : await adapter.GetHealthAsync(entity, ct);
    }
}
