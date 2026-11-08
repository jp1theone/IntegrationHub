using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;

namespace IntegrationHub.Application.Executions;

/// <summary>
/// Adapter contract for a real external provider. Implementations own their HTTP/auth
/// protocol and must obtain secrets from a secret store, never from provider records.
/// </summary>
public interface IProductionIntegrationProvider
{
    ProviderType ProviderType { get; }
    Task<ProviderExecutionResult> ExecuteAsync(Provider provider, IntegrationExecution execution, CancellationToken cancellationToken);
    Task<ProviderHealthResult> GetHealthAsync(Provider provider, CancellationToken cancellationToken);
}
