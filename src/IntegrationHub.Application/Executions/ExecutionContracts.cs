using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;
using IntegrationHub.Domain.Providers;
using IntegrationHub.Application.Common;

namespace IntegrationHub.Application.Executions;

public sealed record ProviderExecutionResult(bool Succeeded, string ResponsePayload, int? HttpStatusCode, string? ErrorCode, string? ErrorMessage, FailureKind FailureKind);
public sealed record ProviderHealthResult(bool Healthy, string Status, string Message, DateTimeOffset CheckedAt);
public sealed record RetryPolicyOptions(int MaxAttempts, int InitialDelaySeconds, double BackoffMultiplier)
{
    public static RetryPolicyOptions Default { get; } = new(3, 5, 2d);
}
public sealed record ExecutionRequest(string Payload, string? IdempotencyKey, string? ActorUserId);
public sealed record ExecutionResponse(Guid Id, Guid CorrelationId, ExecutionStatus Status, string? ResponsePayload, string? ErrorCode, string? ErrorMessage, int AttemptNumber, DateTimeOffset? NextRetryAt, bool IsReplay = false);
public sealed record ExecutionAttemptDto(int AttemptNumber, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, long? DurationMs, bool Succeeded, int? HttpStatusCode, string? ErrorCode, string? ErrorMessage);
public sealed record ExecutionDto(Guid Id, Guid IntegrationId, string IntegrationCode, string ProviderName, Guid CorrelationId, string? IdempotencyKey, string RequestPayload, string? ResponsePayload, ExecutionStatus Status, int AttemptNumber, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, long? DurationMs, int? HttpStatusCode, string? ErrorCode, string? ErrorMessage, DateTimeOffset? NextRetryAt, IReadOnlyList<ExecutionAttemptDto> Attempts);

public interface IIntegrationProvider
{
    Task<ProviderExecutionResult> ExecuteAsync(Provider provider, IntegrationExecution execution, CancellationToken cancellationToken);
    Task<ProviderHealthResult> GetHealthAsync(Provider provider, CancellationToken cancellationToken);
}

public interface IIntegrationCatalog
{
    Task<Integration?> GetIntegrationAsync(Guid id, CancellationToken cancellationToken);
    Task<Integration?> GetIntegrationByCodeAsync(string code, CancellationToken cancellationToken);
}

public interface IExecutionStore
{
    Task<PagedResult<ExecutionDto>> SearchAsync(string? query, Guid? integrationId, ExecutionStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken cancellationToken);
    Task<ExecutionDto?> GetDtoAsync(Guid executionId, CancellationToken cancellationToken);
    Task<IntegrationExecution?> FindSuccessfulByIdempotencyAsync(Guid integrationId, string key, CancellationToken cancellationToken);
    Task<IntegrationExecution?> FindByIdAsync(Guid executionId, CancellationToken cancellationToken);
    Task<bool> TryCreateAsync(IntegrationExecution execution, CancellationToken cancellationToken);
    Task SaveAsync(IntegrationExecution execution, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetDueRetryIdsAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
    Task<bool> TryClaimRetryAsync(Guid executionId, CancellationToken cancellationToken);
    Task<bool> TryClaimManualRetryAsync(Guid executionId, CancellationToken cancellationToken);
}

public interface IOperationalWriter
{
    Task WriteLogAsync(OperationalLogLevel level, string message, Guid? correlationId, Guid? integrationId, Guid? executionId, Guid? providerId, string? errorCode, CancellationToken cancellationToken);
    Task EnsureIncidentAsync(Guid integrationId, Guid executionId, string errorCode, string message, IncidentSeverity severity, CancellationToken cancellationToken);
    Task WriteAuditAsync(string actorUserId, string action, string entityType, string? entityId, string? correlationId, CancellationToken cancellationToken);
}

public sealed class ExecutionException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
