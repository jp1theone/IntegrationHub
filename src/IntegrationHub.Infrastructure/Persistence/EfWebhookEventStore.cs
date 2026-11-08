using IntegrationHub.Application.Common;
using IntegrationHub.Application.Webhooks;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfWebhookEventStore(IntegrationHubDbContext db) : IWebhookEventStore
{
    public Task<WebhookEvent?> FindByExternalIdAsync(Guid integrationId, string eventId, CancellationToken ct) =>
        db.WebhookEvents.SingleOrDefaultAsync(x => x.IntegrationId == integrationId && x.ExternalEventId == eventId, ct);

    public async Task<bool> TryEnqueueAsync(WebhookEvent webhookEvent, CancellationToken ct)
    {
        db.WebhookEvents.Add(webhookEvent);
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException) { db.Entry(webhookEvent).State = EntityState.Detached; return false; }
    }

    public async Task<PagedResult<WebhookEventDto>> SearchAsync(string? query, WebhookEventStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var events = db.WebhookEvents.AsNoTracking().Include(x => x.Integration).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) events = events.Where(x => (x.ExternalEventId != null && EF.Functions.Like(x.ExternalEventId, $"%{query}%")) || x.CorrelationId.ToString().Contains(query) || x.Integration!.Code.Contains(query));
        if (status.HasValue) events = events.Where(x => x.Status == status.Value);
        var total = await events.CountAsync(ct);
        var rows = await events.OrderByDescending(x => x.ReceivedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new WebhookEventDto(x.Id, x.IntegrationId, x.Integration!.Code, x.ExternalEventId, x.CorrelationId, x.Status, x.ReceivedAt, x.ProcessedAt, x.ErrorMessage)).ToListAsync(ct);
        return new PagedResult<WebhookEventDto>(rows, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public Task<WebhookEventDto?> GetDtoAsync(Guid id, CancellationToken ct) => db.WebhookEvents.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new WebhookEventDto(x.Id, x.IntegrationId, x.Integration!.Code, x.ExternalEventId, x.CorrelationId, x.Status, x.ReceivedAt, x.ProcessedAt, x.ErrorMessage)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetQueuedIdsAsync(int take, CancellationToken ct) =>
        await db.WebhookEvents.AsNoTracking().Where(x => x.Status == WebhookEventStatus.Queued).OrderBy(x => x.ReceivedAt).Select(x => x.Id).Take(take).ToListAsync(ct);

    public async Task<bool> TryClaimAsync(Guid id, CancellationToken ct) =>
        await db.WebhookEvents.Where(x => x.Id == id && x.Status == WebhookEventStatus.Queued)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, WebhookEventStatus.Processing), ct) == 1;

    public Task<WebhookEvent?> GetAsync(Guid id, CancellationToken ct) => db.WebhookEvents.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
