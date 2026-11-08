using IntegrationHub.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace IntegrationHub.Infrastructure.Identity;

public sealed class IdentityAuthenticationService(UserManager<IntegrationHubUser> users) : IAuthenticationService
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }
        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email ?? email, user.FullName, roles.ToArray());
    }

    public async Task<AuthenticatedUser?> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive) return null;
        var roles = await users.GetRolesAsync(user);
        return new AuthenticatedUser(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToArray());
    }
}
