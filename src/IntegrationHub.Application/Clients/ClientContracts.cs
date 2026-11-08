using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;

namespace IntegrationHub.Application.Clients;

public sealed record ClientDto(Guid Id, string Code, string Name, string LegalName, string Email, string? Phone, RecordStatus Status, DateTimeOffset CreatedAt, int IntegrationCount);
public sealed record CreateClientRequest(string Code, string Name, string LegalName, string Email, string? Phone);
public sealed record UpdateClientRequest(string Name, string LegalName, string Email, string? Phone);

public interface IClientStore
{
    Task<PagedResult<ClientDto>> SearchAsync(string? query, RecordStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Client?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ClientDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(Client client, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ClientUseCases(IClientStore clients)
{
    public Task<PagedResult<ClientDto>> SearchAsync(string? q, RecordStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, pageSize);
        return clients.SearchAsync(q?.Trim(), status, normalized.Page, normalized.PageSize, ct);
    }

    public Task<ClientDto?> GetAsync(Guid id, CancellationToken ct) => clients.GetDtoAsync(id, ct);

    public async Task<ClientDto> CreateAsync(CreateClientRequest request, CancellationToken ct)
    {
        ValidateEmail(request.Email);
        if (await clients.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), null, ct)) throw new BusinessConflictException("A client with this code already exists.");
        var client = new Client(request.Code, request.Name, request.LegalName, request.Email, request.Phone);
        await clients.AddAsync(client, ct);
        return (await clients.GetDtoAsync(client.Id, ct))!;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct)
    {
        var client = await clients.GetAsync(id, ct);
        if (client is null) return false;
        ValidateEmail(request.Email);
        client.Update(request.Name, request.LegalName, request.Email, request.Phone);
        await clients.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var client = await clients.GetAsync(id, ct);
        if (client is null) return false;
        client.SetActive(active);
        await clients.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); }
        catch (FormatException) { throw new InvalidOperationException("A valid email address is required."); }
    }
}
