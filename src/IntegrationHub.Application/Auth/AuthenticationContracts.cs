namespace IntegrationHub.Application.Auth;

public sealed record AuthenticatedUser(string Id, string Email, string FullName, IReadOnlyCollection<string> Roles);

public interface IAuthenticationService
{
    Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> GetByIdAsync(string userId, CancellationToken cancellationToken);
}

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(AuthenticatedUser user);
}
