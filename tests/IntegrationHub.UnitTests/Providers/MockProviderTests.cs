using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using IntegrationHub.Infrastructure.Providers;

namespace IntegrationHub.UnitTests.Providers;

public sealed class MockProviderTests
{
    [Theory]
    [InlineData(ProviderFailureMode.None, true, 200, FailureKind.None)]
    [InlineData(ProviderFailureMode.Timeout, false, 504, FailureKind.Transient)]
    [InlineData(ProviderFailureMode.Authentication, false, 401, FailureKind.Permanent)]
    [InlineData(ProviderFailureMode.Validation, false, 422, FailureKind.Permanent)]
    [InlineData(ProviderFailureMode.Unavailable, false, 503, FailureKind.Transient)]
    [InlineData(ProviderFailureMode.Http500, false, 500, FailureKind.Transient)]
    public async Task Provider_failure_modes_are_deterministic(ProviderFailureMode mode, bool succeeded, int status, FailureKind kind)
    {
        var provider = new Provider("MOCK", "Mock", "test", ProviderType.GenericRestMock);
        provider.Configure("Mock", "test", 5, mode);
        var execution = new IntegrationExecution(Guid.NewGuid(), "{}", null);
        var adapter = new DeterministicMockIntegrationProvider();
        var first = await adapter.ExecuteAsync(provider, execution, CancellationToken.None);
        var second = await adapter.ExecuteAsync(provider, execution, CancellationToken.None);
        Assert.Equal(succeeded, first.Succeeded);
        Assert.Equal(status, first.HttpStatusCode);
        Assert.Equal(kind, first.FailureKind);
        Assert.Equal(first.ErrorCode, second.ErrorCode);
        Assert.Equal(first.ResponsePayload, second.ResponsePayload);
    }
}
