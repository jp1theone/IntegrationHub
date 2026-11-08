using IntegrationHub.Application.Common;
using IntegrationHub.Application.Observability;
using IntegrationHub.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfLogQueryStore(IntegrationHubDbContext db) : ILogQueryStore
{
    public async Task<PagedResult<IntegrationLogDto>> SearchAsync(string? query, Guid? correlationId, Guid? integrationId, Guid? providerId,
        Guid? executionId, OperationalLogLevel? level, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken ct)
    {
        var logs = db.IntegrationLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) logs = logs.Where(x => EF.Functions.Like(x.Message, $"%{query}%") || (x.ErrorCode != null && EF.Functions.Like(x.ErrorCode, $"%{query}%")));
        if (correlationId.HasValue) logs = logs.Where(x => x.CorrelationId == correlationId.Value);
        if (integrationId.HasValue) logs = logs.Where(x => x.IntegrationId == integrationId.Value);
        if (providerId.HasValue) logs = logs.Where(x => x.ProviderId == providerId.Value);
        if (executionId.HasValue) logs = logs.Where(x => x.ExecutionId == executionId.Value);
        if (level.HasValue) logs = logs.Where(x => x.Level == level.Value);
        if (from.HasValue) logs = logs.Where(x => x.OccurredAt >= from.Value);
        if (to.HasValue) logs = logs.Where(x => x.OccurredAt <= to.Value);
        var total = await logs.CountAsync(ct);
        var items = await logs.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new IntegrationLogDto(x.Id, x.Level, x.Message, x.CorrelationId, x.IntegrationId, x.ExecutionId, x.ProviderId, x.ErrorCode, x.OccurredAt)).ToListAsync(ct);
        return new PagedResult<IntegrationLogDto>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }
}
