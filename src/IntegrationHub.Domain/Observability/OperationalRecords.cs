using IntegrationHub.Domain.Common;

namespace IntegrationHub.Domain.Observability;

public sealed class IntegrationLog
{
    private IntegrationLog() { }
    public IntegrationLog(OperationalLogLevel level, string message, Guid? correlationId = null, Guid? integrationId = null, Guid? executionId = null, Guid? providerId = null, string? errorCode = null)
    { Id = Guid.NewGuid(); Level = level; Message = message; CorrelationId = correlationId; IntegrationId = integrationId; ExecutionId = executionId; ProviderId = providerId; ErrorCode = errorCode; OccurredAt = DateTimeOffset.UtcNow; }
    public Guid Id { get; private set; }
    public OperationalLogLevel Level { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public Guid? CorrelationId { get; private set; }
    public Guid? IntegrationId { get; private set; }
    public Guid? ExecutionId { get; private set; }
    public Guid? ProviderId { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}

public sealed class AuditLog
{
    private AuditLog() { }
    public AuditLog(string actorUserId, string action, string entityType, string? entityId, string? correlationId)
    { Id = Guid.NewGuid(); ActorUserId = actorUserId; Action = action; EntityType = entityType; EntityId = entityId; CorrelationId = correlationId; OccurredAt = DateTimeOffset.UtcNow; }
    public Guid Id { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
