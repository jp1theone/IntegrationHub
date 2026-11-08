using System.Text.Json;
using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;

namespace IntegrationHub.Infrastructure.Providers;

public sealed class DeterministicMockIntegrationProvider : IIntegrationProvider
{
    public Task<ProviderExecutionResult> ExecuteAsync(Provider provider, IntegrationExecution execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failure = provider.FailureMode switch
        {
            ProviderFailureMode.Timeout => new ProviderExecutionResult(false, "{}", 504, "PROVIDER_TIMEOUT", "The simulated provider timed out.", FailureKind.Transient),
            ProviderFailureMode.Authentication => new ProviderExecutionResult(false, "{}", 401, "PROVIDER_AUTHENTICATION", "The simulated provider rejected authentication.", FailureKind.Permanent),
            ProviderFailureMode.Validation => new ProviderExecutionResult(false, "{}", 422, "PROVIDER_VALIDATION", "The simulated provider rejected the request payload.", FailureKind.Permanent),
            ProviderFailureMode.Unavailable => new ProviderExecutionResult(false, "{}", 503, "PROVIDER_UNAVAILABLE", "The simulated provider is temporarily unavailable.", FailureKind.Transient),
            ProviderFailureMode.Http500 => new ProviderExecutionResult(false, "{}", 500, "PROVIDER_HTTP_500", "The simulated provider returned HTTP 500.", FailureKind.Transient),
            _ => new ProviderExecutionResult(true, JsonSerializer.Serialize(new
            {
                provider = provider.Code,
                executionId = execution.Id,
                correlationId = execution.CorrelationId,
                outcome = "accepted",
                simulated = true
            }), 200, null, null, FailureKind.None)
        };
        return Task.FromResult(failure);
    }

    public Task<ProviderHealthResult> GetHealthAsync(Provider provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var healthy = provider.Status == RecordStatus.Active && provider.FailureMode is not (ProviderFailureMode.Timeout or ProviderFailureMode.Unavailable or ProviderFailureMode.Http500);
        var status = healthy ? "Healthy" : "Unhealthy";
        return Task.FromResult(new ProviderHealthResult(healthy, status, healthy ? "Mock provider is ready." : "Mock provider is configured to simulate an outage.", DateTimeOffset.UtcNow));
    }
}
