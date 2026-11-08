using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;

namespace IntegrationHub.Application.Integrations;

public sealed record IntegrationDto(Guid Id, string Code, string Name, string Description, Guid ClientId, string ClientName, Guid ProviderId, string ProviderName, IntegrationType Type, IntegrationStatus Status, IntegrationEnvironment Environment, DateTimeOffset CreatedAt);
public sealed record CreateIntegrationRequest(Guid ClientId, Guid ProviderId, string Code, string Name, string Description, IntegrationType Type, IntegrationEnvironment Environment);
public sealed record UpdateIntegrationRequest(string Name, string Description, IntegrationType Type, IntegrationEnvironment Environment);

public interface IIntegrationStore
{
    Task<PagedResult<IntegrationDto>> SearchAsync(string? query, Guid? clientId, Guid? providerId, IntegrationStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Integration?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IntegrationDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> ClientExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ProviderExistsAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Integration integration, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class IntegrationUseCases(IIntegrationStore integrations)
{
    public Task<PagedResult<IntegrationDto>> SearchAsync(string? q, Guid? clientId, Guid? providerId, IntegrationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, pageSize);
        return integrations.SearchAsync(q?.Trim(), clientId, providerId, status, normalized.Page, normalized.PageSize, ct);
    }
    public Task<IntegrationDto?> GetAsync(Guid id, CancellationToken ct) => integrations.GetDtoAsync(id, ct);
    public async Task<IntegrationDto> CreateAsync(CreateIntegrationRequest request, CancellationToken ct)
    {
        if (!await integrations.ClientExistsAsync(request.ClientId, ct)) throw new BusinessConflictException("The client does not exist.");
        if (!await integrations.ProviderExistsAsync(request.ProviderId, ct)) throw new BusinessConflictException("The provider does not exist.");
        if (await integrations.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), null, ct)) throw new BusinessConflictException("An integration with this code already exists.");
        var entity = new Integration(request.ClientId, request.ProviderId, request.Code, request.Name, request.Description, request.Type, request.Environment);
        await integrations.AddAsync(entity, ct);
        return (await integrations.GetDtoAsync(entity.Id, ct))!;
    }
    public async Task<bool> UpdateAsync(Guid id, UpdateIntegrationRequest request, CancellationToken ct)
    {
        var entity = await integrations.GetAsync(id, ct); if (entity is null) return false;
        entity.Update(request.Name, request.Description, request.Type, request.Environment); await integrations.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> SetStatusAsync(Guid id, IntegrationStatus status, CancellationToken ct)
    {
        var entity = await integrations.GetAsync(id, ct); if (entity is null) return false;
        entity.SetStatus(status); await integrations.SaveChangesAsync(ct); return true;
    }
}
