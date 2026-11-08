using System.Reflection;
using IntegrationHub.Application.Common;
using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Clients;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using Moq;

namespace IntegrationHub.UnitTests.Application;

public sealed class ExecuteIntegrationUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Successful_idempotency_key_replays_previous_result_without_calling_provider()
    {
        var (useCase, catalog, executions, provider, _, integration) = CreateSubject();
        var previous = new IntegrationExecution(integration.Id, "{\"reference\":\"same\"}", "order-42");
        previous.BeginAttempt(Now);
        previous.CompleteSuccess("{\"accepted\":true}", 200, Now.AddMilliseconds(40));
        executions.Setup(x => x.FindSuccessfulByIdempotencyAsync(integration.Id, "order-42", It.IsAny<CancellationToken>())).ReturnsAsync(previous);

        var response = await useCase.ExecuteAsync(integration.Id, new ExecutionRequest("{\"different\":true}", "order-42", null), CancellationToken.None);

        Assert.True(response.IsReplay);
        Assert.Equal(previous.Id, response.Id);
        Assert.Equal(ExecutionStatus.Succeeded, response.Status);
        executions.Verify(x => x.TryCreateAsync(It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()), Times.Never);
        provider.Verify(x => x.ExecuteAsync(It.IsAny<Provider>(), It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()), Times.Never);
        catalog.Verify(x => x.GetIntegrationAsync(integration.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transient_failure_is_scheduled_with_exponential_backoff_and_attempt_history()
    {
        var (useCase, _, executions, provider, operations, integration) = CreateSubject();
        IntegrationExecution? saved = null;
        executions.Setup(x => x.TryCreateAsync(It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()))
            .Callback<IntegrationExecution, CancellationToken>((execution, _) => saved = execution).ReturnsAsync(true);
        executions.Setup(x => x.SaveAsync(It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.ExecuteAsync(integration.Provider!, It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderExecutionResult(false, "{}", 503, "PROVIDER_UNAVAILABLE", "Try again", FailureKind.Transient));
        operations.Setup(x => x.WriteLogAsync(It.IsAny<OperationalLogLevel>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var response = await useCase.ExecuteAsync(integration.Id, new ExecutionRequest("{}", "retry-case", null), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(ExecutionStatus.RetryScheduled, response.Status);
        Assert.Equal(Now.AddSeconds(5), response.NextRetryAt);
        Assert.Single(saved!.Attempts);
        Assert.Equal(1, saved.AttemptNumber);
        operations.Verify(x => x.EnsureIncidentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IncidentSeverity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Permanent_provider_failure_is_not_scheduled_for_automatic_retry()
    {
        var (useCase, _, executions, provider, operations, integration) = CreateSubject();
        IntegrationExecution? saved = null;
        executions.Setup(x => x.TryCreateAsync(It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()))
            .Callback<IntegrationExecution, CancellationToken>((execution, _) => saved = execution).ReturnsAsync(true);
        executions.Setup(x => x.SaveAsync(It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        provider.Setup(x => x.ExecuteAsync(integration.Provider!, It.IsAny<IntegrationExecution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderExecutionResult(false, "{}", 400, "VALIDATION", "Invalid data", FailureKind.Permanent));
        operations.Setup(x => x.WriteLogAsync(It.IsAny<OperationalLogLevel>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        operations.Setup(x => x.EnsureIncidentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IncidentSeverity>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var response = await useCase.ExecuteAsync(integration.Id, new ExecutionRequest("{}", null, null), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(ExecutionStatus.Failed, response.Status);
        Assert.Null(response.NextRetryAt);
        Assert.Single(saved!.Attempts);
        operations.Verify(x => x.EnsureIncidentAsync(integration.Id, saved!.Id, "VALIDATION", "Invalid data", IncidentSeverity.Medium, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (ExecuteIntegrationUseCase UseCase, Mock<IIntegrationCatalog> Catalog, Mock<IExecutionStore> Executions,
        Mock<IIntegrationProvider> Provider, Mock<IOperationalWriter> Operations, Integration Integration) CreateSubject()
    {
        var client = new Client("client-1", "Fictional Client", "Fictional Client LLC", "ops@example.test", null);
        var providerEntity = new Provider("mock-1", "Mock Provider", "For tests", ProviderType.GenericRestMock);
        var integration = new Integration(client.Id, providerEntity.Id, "int-1", "Integration", "For tests", IntegrationType.RestApi, IntegrationEnvironment.Testing);
        typeof(Integration).GetProperty(nameof(Integration.Client))!.SetValue(integration, client);
        typeof(Integration).GetProperty(nameof(Integration.Provider))!.SetValue(integration, providerEntity);
        var catalog = new Mock<IIntegrationCatalog>();
        catalog.Setup(x => x.GetIntegrationAsync(integration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(integration);
        var executions = new Mock<IExecutionStore>();
        executions.Setup(x => x.FindSuccessfulByIdempotencyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IntegrationExecution?)null);
        var provider = new Mock<IIntegrationProvider>();
        var operations = new Mock<IOperationalWriter>();
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(Now);
        var useCase = new ExecuteIntegrationUseCase(catalog.Object, executions.Object, provider.Object, operations.Object, clock.Object, new RetryPolicyOptions(3, 5, 2));
        return (useCase, catalog, executions, provider, operations, integration);
    }
}
