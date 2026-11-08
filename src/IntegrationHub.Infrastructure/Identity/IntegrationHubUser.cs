using Microsoft.AspNetCore.Identity;

namespace IntegrationHub.Infrastructure.Identity;

public sealed class IntegrationHubUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
