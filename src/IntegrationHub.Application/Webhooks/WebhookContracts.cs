using IntegrationHub.Application.Common;
using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Webhooks;

namespace IntegrationHub.Application.Webhooks;

public sealed record WebhookEventDto(Guid Id, Guid IntegrationId, string IntegrationCode, string? ExternalEventId, Guid CorrelationId, WebhookEventStatus Status, DateTimeOffset ReceivedAt, DateTimeOffset? ProcessedAt, string? ErrorMessage);
public sealed record WebhookReceiveResult(Guid Id, Guid CorrelationId, WebhookEventStatus Status, bool Duplicate);

public interface IWebhookEventStore
{
    Task<WebhookEvent?> FindByExternalIdAsync(Guid integrationId, string eventId, CancellationToken cancellationToken);
    Task<bool> TryEnqueueAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken);
    Task<PagedResult<WebhookEventDto>> SearchAsync(string? query, WebhookEventStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<WebhookEventDto?> GetDtoAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetQueuedIdsAsync(int take, CancellationToken cancellationToken);
    Task<bool> TryClaimAsync(Guid id, CancellationToken cancellationToken);
    Task<WebhookEvent?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class WebhookUseCases(IIntegrationCatalog integrations, IWebhookEventStore events, ExecuteIntegrationUseCase executions)
{
    public async Task<WebhookReceiveResult> ReceiveAsync(string integrationCode, string externalEventId, string payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalEventId) || externalEventId.Length > 200) throw new ExecutionException("A valid X-Event-ID header is required.", 400);
        if (string.IsNullOrWhiteSpace(payload)) throw new ExecutionException("Webhook payload is required.", 400);
        var integration = await integrations.GetIntegrationByCodeAsync(integrationCode, ct)
            ?? throw new ExecutionException("Integration was not found.", 404);
        if (integration.Type != IntegrationType.Webhook) throw new ExecutionException("The integration is not configured to receive webhooks.", 409);
        if (integration.Provider is null || integration.Client is null || !integration.CanExecute)
            throw new ExecutionException("The integration, client, or provider is inactive.", 409);
        var previous = await events.FindByExternalIdAsync(integration.Id, externalEventId, ct);
        if (previous is not null) return new WebhookReceiveResult(previous.Id, previous.CorrelationId, WebhookEventStatus.Duplicate, true);
        var webhook = new WebhookEvent(integration.Id, externalEventId, Guid.NewGuid(), payload);
        if (!await events.TryEnqueueAsync(webhook, ct))
        {
            previous = await events.FindByExternalIdAsync(integration.Id, externalEventId, ct);
            if (previous is not null) return new WebhookReceiveResult(previous.Id, previous.CorrelationId, WebhookEventStatus.Duplicate, true);
            throw new ExecutionException("The webhook event could not be queued.", 409);
        }
        return new WebhookReceiveResult(webhook.Id, webhook.CorrelationId, webhook.Status, false);
    }

    public Task<PagedResult<WebhookEventDto>> SearchAsync(string? q, WebhookEventStatus? status, int page, int size, CancellationToken ct)
    {
        var normalized = PageLimits.Normalize(page, size);
        return events.SearchAsync(q?.Trim(), status, normalized.Page, normalized.PageSize, ct);
    }
    public Task<WebhookEventDto?> GetAsync(Guid id, CancellationToken ct) => events.GetDtoAsync(id, ct);

    public async Task ProcessQueuedAsync(Guid id, CancellationToken ct)
    {
        if (!await events.TryClaimAsync(id, ct)) return;
        var webhook = await events.GetAsync(id, ct);
        if (webhook is null) return;
        webhook.MarkProcessing();
        await events.SaveChangesAsync(ct);
        try
        {
            await executions.ExecuteAsync(webhook.IntegrationId, new ExecutionRequest(webhook.Payload, webhook.ExternalEventId, null), ct);
            webhook.MarkProcessed(DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            webhook.MarkFailed("Event processing failed; inspect the correlated execution and logs.", DateTimeOffset.UtcNow);
        }
        await events.SaveChangesAsync(ct);
    }
}
