using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IntegrationHub.Infrastructure.Providers;

public sealed class ModeAwareIntegrationProvider(
    IOptions<IntegrationRuntimeOptions> options,
    DeterministicMockIntegrationProvider simulator,
    IEnumerable<IProductionIntegrationProvider> productionAdapters) : IIntegrationProvider
{
    private readonly IReadOnlyDictionary<ProviderType, IProductionIntegrationProvider> adapters =
        productionAdapters.ToDictionary(adapter => adapter.ProviderType);

    public Task<ProviderExecutionResult> ExecuteAsync(Provider provider, IntegrationExecution execution, CancellationToken cancellationToken)
    {
        if (options.Value.Mode == IntegrationRuntimeMode.Simulation)
            return simulator.ExecuteAsync(provider, execution, cancellationToken);

        if (!HasAllowedProductionEndpoint(provider))
            return Task.FromResult(new ProviderExecutionResult(
                false,
                "{}",
                StatusCodes.Status503ServiceUnavailable,
                "PRODUCTION_ENDPOINT_NOT_ALLOWED",
                "Production execution is blocked. Configure an HTTPS provider BaseUrl whose hostname is explicitly allowlisted by the API.",
                FailureKind.Permanent));

        if (adapters.TryGetValue(provider.Type, out var adapter))
            return adapter.ExecuteAsync(provider, execution, cancellationToken);

        return Task.FromResult(new ProviderExecutionResult(
            false,
            "{}",
            StatusCodes.Status503ServiceUnavailable,
            "PRODUCTION_ADAPTER_NOT_CONFIGURED",
            $"No production adapter is registered for provider type '{provider.Type}'. This request was not sent to an external system.",
            FailureKind.Permanent));
    }

    public Task<ProviderHealthResult> GetHealthAsync(Provider provider, CancellationToken cancellationToken)
    {
        if (options.Value.Mode == IntegrationRuntimeMode.Simulation)
            return simulator.GetHealthAsync(provider, cancellationToken);

        if (!HasAllowedProductionEndpoint(provider))
            return Task.FromResult(new ProviderHealthResult(
                false,
                "EndpointNotAllowed",
                "Configure an HTTPS provider BaseUrl and explicitly allowlist its hostname before enabling external health checks.",
                DateTimeOffset.UtcNow));

        if (adapters.TryGetValue(provider.Type, out var adapter))
            return adapter.GetHealthAsync(provider, cancellationToken);

        return Task.FromResult(new ProviderHealthResult(
            false,
            "AdapterNotConfigured",
            $"Production mode is selected, but no adapter is registered for '{provider.Type}'. No external connection was attempted.",
            DateTimeOffset.UtcNow));
    }

    private bool HasAllowedProductionEndpoint(Provider provider)
    {
        if (!Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return options.Value.AllowedHosts.Any(host => string.Equals(host, uri.IdnHost, StringComparison.OrdinalIgnoreCase));
    }
}
