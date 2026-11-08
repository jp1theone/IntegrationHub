namespace IntegrationHub.Application.Auth;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string IntegrationAnalyst = "Integration Analyst";
    public const string SupportAnalyst = "Support Analyst";
    public const string ReadOnly = "ReadOnly";
    public static readonly string[] All = [Administrator, IntegrationAnalyst, SupportAnalyst, ReadOnly];
}
