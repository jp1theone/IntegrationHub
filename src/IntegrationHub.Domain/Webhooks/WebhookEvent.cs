using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Integrations;

namespace IntegrationHub.Domain.Webhooks;

public sealed class WebhookEvent
{
    private WebhookEvent() { }
    public WebhookEvent(Guid integrationId, string? externalEventId, Guid correlationId, string payload)
    {
        if (integrationId == Guid.Empty) throw new DomainException("Integration is required.");
        if (string.IsNullOrWhiteSpace(payload)) throw new DomainException("Webhook payload is required.");
        Id = Guid.NewGuid(); IntegrationId = integrationId; ExternalEventId = externalEventId?.Trim();
        CorrelationId = correlationId; Payload = payload; Status = WebhookEventStatus.Queued; ReceivedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid IntegrationId { get; private set; }
    public string? ExternalEventId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string Payload { get; private set; } = "{}";
    public WebhookEventStatus Status { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Integration? Integration { get; private set; }
    public void MarkProcessing() => Status = WebhookEventStatus.Processing;
    public void MarkProcessed(DateTimeOffset now) { Status = WebhookEventStatus.Processed; ProcessedAt = now; }
    public void MarkFailed(string message, DateTimeOffset now) { Status = WebhookEventStatus.Failed; ErrorMessage = message; ProcessedAt = now; }
}
