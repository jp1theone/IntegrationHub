using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;

namespace IntegrationHub.UnitTests.Domain;

public sealed class ExecutionTests
{
    [Fact]
    public void Transient_failure_can_be_scheduled_and_attempt_history_is_retained()
    {
        var execution = new IntegrationExecution(Guid.NewGuid(), "{\"amount\":12}", "request-1");
        var started = DateTimeOffset.UtcNow;
        execution.BeginAttempt(started);
        var attempt = new ExecutionAttempt(execution.Id, execution.AttemptNumber, started);
        execution.CompleteFailure("PROVIDER_UNAVAILABLE", "Try later", 503, FailureKind.Transient, started.AddMilliseconds(50));
        execution.ScheduleRetry(started.AddSeconds(5));
        attempt.Finish(false, 503, "PROVIDER_UNAVAILABLE", "Try later", started.AddMilliseconds(50));
        execution.Attempts.Add(attempt);
        Assert.Equal(ExecutionStatus.RetryScheduled, execution.Status);
        Assert.Single(execution.Attempts);
        Assert.Equal("request-1", execution.IdempotencyKey);
        Assert.NotNull(execution.NextRetryAt);
    }

    [Fact]
    public void Permanent_failure_cannot_be_scheduled_for_automatic_retry()
    {
        var execution = new IntegrationExecution(Guid.NewGuid(), "{}", null);
        execution.BeginAttempt(DateTimeOffset.UtcNow);
        execution.CompleteFailure("VALIDATION", "Invalid request", 422, FailureKind.Permanent, DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => execution.ScheduleRetry(DateTimeOffset.UtcNow.AddSeconds(5)));
    }

    [Fact]
    public void Succeeded_execution_cannot_be_started_again()
    {
        var execution = new IntegrationExecution(Guid.NewGuid(), "{}", null);
        execution.BeginAttempt(DateTimeOffset.UtcNow);
        execution.CompleteSuccess("{\"ok\":true}", 200, DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => execution.BeginAttempt(DateTimeOffset.UtcNow));
    }
}
