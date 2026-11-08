namespace IntegrationHub.Infrastructure.Providers;

public enum IntegrationRuntimeMode
{
    Simulation,
    Production
}

public sealed class IntegrationRuntimeOptions
{
    public IntegrationRuntimeMode Mode { get; set; } = IntegrationRuntimeMode.Simulation;
    public string[] AllowedHosts { get; set; } = [];
}
