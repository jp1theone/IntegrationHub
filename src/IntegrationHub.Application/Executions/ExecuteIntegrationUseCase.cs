using IntegrationHub.Application.Common;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Observability;

namespace IntegrationHub.Application.Executions;

public sealed class ExecuteIntegrationUseCase(
    IIntegrationCatalog catalog,
    IExecutionStore executions,
    IIntegrationProvider provider,
    IOperationalWriter operations,
    IClock clock,
    RetryPolicyOptions retryOptions)
{
    public async Task<ExecutionResponse> ExecuteAsync(Guid integrationId, ExecutionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Payload)) throw new ExecutionException("Payload is required.", 400);
        var integration = await catalog.GetIntegrationAsync(integrationId, cancellationToken)
            ?? throw new ExecutionException("Integration was not found.", 404);
        if (integration.Provider is null || integration.Client is null || !integration.CanExecute)
            throw new ExecutionException("The integration, client, or provider is inactive.", 409);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var previous = await executions.FindSuccessfulByIdempotencyAsync(integrationId, request.IdempotencyKey, cancellationToken);
            if (previous is not null) return ToResponse(previous, true);
        }

        var execution = new IntegrationExecution(integrationId, request.Payload, request.IdempotencyKey);
        execution.RecordActor(request.ActorUserId);
        if (!await executions.TryCreateAsync(execution, cancellationToken))
        {
            var previous = request.IdempotencyKey is null ? null : await executions.FindSuccessfulByIdempotencyAsync(integrationId, request.IdempotencyKey, cancellationToken);
            if (previous is not null) return ToResponse(previous, true);
            throw new ExecutionException("A request with this idempotency key is already being processed.", 409);
        }

        await ProcessAsync(execution, integration, cancellationToken);
        return ToResponse(execution);
    }

    public async Task<bool> RetryAsync(Guid executionId, string? actorUserId, CancellationToken cancellationToken)
    {
        var execution = await executions.FindByIdAsync(executionId, cancellationToken)
            ?? throw new ExecutionException("Execution was not found.", 404);
        var integration = await catalog.GetIntegrationAsync(execution.IntegrationId, cancellationToken)
            ?? throw new ExecutionException("Integration was not found.", 404);
        if (integration.Provider is null || !integration.CanExecute) throw new ExecutionException("The integration or provider is inactive.", 409);
        if (execution.Status is not (ExecutionStatus.Failed or ExecutionStatus.DeadLetter)) throw new ExecutionException("Only failed executions can be retried manually.", 409);
        if (!await executions.TryClaimManualRetryAsync(executionId, cancellationToken))
            throw new ExecutionException("This execution is already being retried.", 409);

        execution.RecordActor(actorUserId);
        execution.BeginAttempt(clock.UtcNow);
        await executions.SaveAsync(execution, cancellationToken);
        await RunAttemptAsync(execution, integration, cancellationToken);
        return true;
    }

    public async Task ProcessScheduledRetryAsync(Guid executionId, CancellationToken cancellationToken)
    {
        if (!await executions.TryClaimRetryAsync(executionId, cancellationToken)) return;
        var execution = await executions.FindByIdAsync(executionId, cancellationToken);
        if (execution is null) return;
        var integration = await catalog.GetIntegrationAsync(execution.IntegrationId, cancellationToken);
        if (integration?.Provider is null || !integration.CanExecute)
        {
            execution.CompleteFailure("INTEGRATION_INACTIVE", "The integration or its client/provider is inactive.", null, FailureKind.Permanent, clock.UtcNow);
            execution.MarkDeadLetter(clock.UtcNow);
            await executions.SaveAsync(execution, cancellationToken);
            return;
        }
        execution.BeginAttempt(clock.UtcNow);
        await executions.SaveAsync(execution, cancellationToken);
        await RunAttemptAsync(execution, integration, cancellationToken);
    }

    private async Task ProcessAsync(IntegrationExecution execution, Integration integration, CancellationToken cancellationToken)
    {
        execution.BeginAttempt(clock.UtcNow);
        await executions.SaveAsync(execution, cancellationToken);
        await RunAttemptAsync(execution, integration, cancellationToken);
    }

    private async Task RunAttemptAsync(IntegrationExecution execution, Integration integration, CancellationToken cancellationToken)
    {
        var started = clock.UtcNow;
        var attempt = new ExecutionAttempt(execution.Id, execution.AttemptNumber, started);
        try
        {
            var result = await provider.ExecuteAsync(integration.Provider!, execution, cancellationToken);
            var completed = clock.UtcNow;
            attempt.Finish(result.Succeeded, result.HttpStatusCode, result.ErrorCode, result.ErrorMessage, completed);
            if (result.Succeeded)
            {
                execution.CompleteSuccess(result.ResponsePayload, result.HttpStatusCode ?? 200, completed);
                await operations.WriteLogAsync(OperationalLogLevel.Information, "Integration execution succeeded.", execution.CorrelationId, integration.Id, execution.Id, integration.ProviderId, null, cancellationToken);
            }
            else
            {
                execution.CompleteFailure(result.ErrorCode ?? "PROVIDER_ERROR", result.ErrorMessage ?? "The provider rejected the request.", result.HttpStatusCode, result.FailureKind, completed);
                if (result.FailureKind == FailureKind.Transient && execution.AttemptNumber < retryOptions.MaxAttempts)
                {
                    var delay = retryOptions.InitialDelaySeconds * Math.Pow(retryOptions.BackoffMultiplier, execution.AttemptNumber - 1);
                    execution.ScheduleRetry(completed.AddSeconds(delay));
                }
                else if (result.FailureKind == FailureKind.Transient)
                {
                    execution.MarkDeadLetter(completed);
                    await operations.EnsureIncidentAsync(integration.Id, execution.Id, result.ErrorCode ?? "RETRY_LIMIT", result.ErrorMessage ?? "Retry limit reached.", IncidentSeverity.High, cancellationToken);
                }
                else
                {
                    await operations.EnsureIncidentAsync(integration.Id, execution.Id, result.ErrorCode ?? "PROVIDER_ERROR", result.ErrorMessage ?? "Permanent provider error.", IncidentSeverity.Medium, cancellationToken);
                }
                await operations.WriteLogAsync(result.FailureKind == FailureKind.Transient ? OperationalLogLevel.Warning : OperationalLogLevel.Error, result.ErrorMessage ?? "Integration execution failed.", execution.CorrelationId, integration.Id, execution.Id, integration.ProviderId, result.ErrorCode, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var completed = clock.UtcNow;
            execution.CompleteFailure("EXECUTION_CANCELLED", "Execution was cancelled.", null, FailureKind.Transient, completed);
            attempt.Finish(false, null, "EXECUTION_CANCELLED", "Execution was cancelled.", completed);
            if (execution.AttemptNumber < retryOptions.MaxAttempts)
                execution.ScheduleRetry(completed.AddSeconds(retryOptions.InitialDelaySeconds * Math.Pow(retryOptions.BackoffMultiplier, execution.AttemptNumber - 1)));
            else execution.MarkDeadLetter(completed);
            execution.Attempts.Add(attempt);
            await executions.SaveAsync(execution, CancellationToken.None);
            return;
        }
        catch (Exception)
        {
            var completed = clock.UtcNow;
            attempt.Finish(false, null, "UNEXPECTED_PROVIDER_ERROR", "Provider execution failed unexpectedly.", completed);
            execution.CompleteFailure("UNEXPECTED_PROVIDER_ERROR", "Provider execution failed unexpectedly.", null, FailureKind.Transient, completed);
            if (execution.AttemptNumber < retryOptions.MaxAttempts)
            {
                var delay = retryOptions.InitialDelaySeconds * Math.Pow(retryOptions.BackoffMultiplier, execution.AttemptNumber - 1);
                execution.ScheduleRetry(completed.AddSeconds(delay));
            }
            else execution.MarkDeadLetter(completed);
            await operations.WriteLogAsync(OperationalLogLevel.Error, "Provider execution failed unexpectedly.", execution.CorrelationId, integration.Id, execution.Id, integration.ProviderId, "UNEXPECTED_PROVIDER_ERROR", cancellationToken);
        }
        execution.Attempts.Add(attempt);
        await executions.SaveAsync(execution, cancellationToken);
    }

    private static ExecutionResponse ToResponse(IntegrationExecution execution, bool replay = false) =>
        new(execution.Id, execution.CorrelationId, execution.Status, execution.ResponsePayload, execution.ErrorCode, execution.ErrorMessage, execution.AttemptNumber, execution.NextRetryAt, replay);
}
