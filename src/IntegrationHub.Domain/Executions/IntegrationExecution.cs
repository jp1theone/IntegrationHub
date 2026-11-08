using IntegrationHub.Domain.Common;

namespace IntegrationHub.Domain.Integrations;

public sealed class IntegrationExecution
{
    private IntegrationExecution() { }

    public IntegrationExecution(Guid integrationId, string requestPayload, string? idempotencyKey)
    {
        if (integrationId == Guid.Empty) throw new DomainException("Integration is required.");
        if (string.IsNullOrWhiteSpace(requestPayload)) throw new DomainException("Request payload is required.");
        Id = Guid.NewGuid();
        IntegrationId = integrationId;
        CorrelationId = Guid.NewGuid();
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        RequestPayload = requestPayload;
        Status = ExecutionStatus.Pending;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string RequestPayload { get; private set; } = "{}";
    public string? ResponsePayload { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public FailureKind FailureKind { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public string? LastActorUserId { get; private set; }
    public Integration? Integration { get; private set; }
    public ICollection<ExecutionAttempt> Attempts { get; private set; } = new List<ExecutionAttempt>();

    public void BeginAttempt(DateTimeOffset now, bool allowDeadLetter = false)
    {
        if (Status == ExecutionStatus.Succeeded || (Status == ExecutionStatus.DeadLetter && !allowDeadLetter)) throw new DomainException("This execution cannot be processed again.");
        if (Status == ExecutionStatus.DeadLetter) Status = ExecutionStatus.Failed;
        Status = ExecutionStatus.Processing;
        AttemptNumber++;
        NextRetryAt = null;
        StartedAt = now;
    }

    public void CompleteSuccess(string response, int httpStatus, DateTimeOffset now)
    {
        Status = ExecutionStatus.Succeeded;
        ResponsePayload = response;
        HttpStatusCode = httpStatus;
        ErrorCode = null; ErrorMessage = null; FailureKind = FailureKind.None;
        CompletedAt = now; DurationMs = Math.Max(0, (long)(now - StartedAt).TotalMilliseconds);
        NextRetryAt = null;
    }

    public void CompleteFailure(string code, string message, int? httpStatus, FailureKind kind, DateTimeOffset now)
    {
        Status = ExecutionStatus.Failed;
        ErrorCode = code; ErrorMessage = message; HttpStatusCode = httpStatus; FailureKind = kind;
        CompletedAt = now; DurationMs = Math.Max(0, (long)(now - StartedAt).TotalMilliseconds);
    }

    public void ScheduleRetry(DateTimeOffset nextRetryAt)
    {
        if (FailureKind != FailureKind.Transient || Status != ExecutionStatus.Failed) throw new DomainException("Only transient failures can be retried automatically.");
        Status = ExecutionStatus.RetryScheduled;
        NextRetryAt = nextRetryAt;
    }

    public void MarkDeadLetter(DateTimeOffset now)
    {
        Status = ExecutionStatus.DeadLetter;
        NextRetryAt = null;
        CompletedAt ??= now;
    }

    public void RecordActor(string? userId) => LastActorUserId = userId;
}

public sealed class ExecutionAttempt
{
    private ExecutionAttempt() { }
    public ExecutionAttempt(Guid executionId, int attemptNumber, DateTimeOffset startedAt)
    { Id = Guid.NewGuid(); ExecutionId = executionId; AttemptNumber = attemptNumber; StartedAt = startedAt; }
    public Guid Id { get; private set; }
    public Guid ExecutionId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }
    public bool Succeeded { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public void Finish(bool succeeded, int? httpStatusCode, string? errorCode, string? errorMessage, DateTimeOffset at)
    { Succeeded = succeeded; HttpStatusCode = httpStatusCode; ErrorCode = errorCode; ErrorMessage = errorMessage; CompletedAt = at; DurationMs = Math.Max(0, (long)(at - StartedAt).TotalMilliseconds); }
}
