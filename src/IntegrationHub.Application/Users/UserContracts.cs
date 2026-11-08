using IntegrationHub.Application.Common;

namespace IntegrationHub.Application.Users;

public sealed record UserDto(string Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles, DateTimeOffset CreatedAt);
public sealed record CreateUserRequest(string Email, string FullName, string Password, string Role);
public sealed record UpdateUserRequest(string Email, string FullName, string Role);

public interface IUserManagementStore
{
    Task<PagedResult<UserDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken cancellationToken);
    Task<UserDto?> GetAsync(string id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<bool> SetActiveAsync(string id, bool active, CancellationToken cancellationToken);
}

public sealed class UserManagementUseCases(IUserManagementStore users)
{
    public Task<PagedResult<UserDto>> SearchAsync(string? q, int page, int size, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, size); return users.SearchAsync(q?.Trim(), normalized.Page, normalized.PageSize, ct);
    }
    public Task<UserDto?> GetAsync(string id, CancellationToken ct) => users.GetAsync(id, ct);
    public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct) => users.CreateAsync(request with { Email = request.Email.Trim().ToLowerInvariant(), FullName = request.FullName.Trim() }, ct);
    public Task<bool> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request with { Email = request.Email.Trim().ToLowerInvariant(), FullName = request.FullName.Trim() }, ct);
    public Task<bool> SetActiveAsync(string id, bool active, CancellationToken ct) => users.SetActiveAsync(id, active, ct);
}
