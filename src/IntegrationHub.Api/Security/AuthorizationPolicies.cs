using IntegrationHub.Application.Auth;

namespace IntegrationHub.Api.Security;

public static class AuthorizationPolicies
{
    public const string ViewOperations = "ViewOperations";
    public const string ManageIntegrations = "ManageIntegrations";
    public const string ManageUsers = "ManageUsers";
    public const string RetryExecution = "RetryExecution";

    public static void AddIntegrationHubPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ViewOperations, policy => policy.RequireRole(RoleNames.Administrator, RoleNames.IntegrationAnalyst, RoleNames.SupportAnalyst, RoleNames.ReadOnly));
            options.AddPolicy(ManageIntegrations, policy => policy.RequireRole(RoleNames.Administrator, RoleNames.IntegrationAnalyst));
            options.AddPolicy(ManageUsers, policy => policy.RequireRole(RoleNames.Administrator));
            options.AddPolicy(RetryExecution, policy => policy.RequireRole(RoleNames.Administrator, RoleNames.IntegrationAnalyst, RoleNames.SupportAnalyst));
        });
    }
}
