using IntegrationHub.Application.Reporting;
using IntegrationHub.Domain.Common;
using IntegrationHub.Domain.Incidents;
using IntegrationHub.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence;

public sealed class EfReportingStore(IntegrationHubDbContext db) : IReportingStore
{
    public async Task<DashboardSummaryDto> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, decimal target, int maxAverageMs, CancellationToken ct)
    {
        var executions = db.Executions.AsNoTracking().Where(x => x.StartedAt >= from && x.StartedAt <= to &&
            (x.Status == ExecutionStatus.Succeeded || x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter));
        var total = await executions.CountAsync(ct);
        var succeeded = await executions.CountAsync(x => x.Status == ExecutionStatus.Succeeded, ct);
        var failed = await executions.CountAsync(x => x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter, ct);
        var durations = await executions.Where(x => x.DurationMs != null).Select(x => x.DurationMs!.Value).ToListAsync(ct);
        var average = durations.Count == 0 ? null : (decimal?)durations.Average();
        var p95 = Percentile95(durations);
        var successRate = total == 0 ? null : (decimal?)Math.Round(succeeded * 100m / total, 2);
        var compliant = successRate.HasValue && average.HasValue && successRate >= target && average <= maxAverageMs;
        var open = await db.Incidents.CountAsync(x => x.Status == IncidentStatus.Open || x.Status == IncidentStatus.InProgress, ct);
        var critical = await db.Incidents.CountAsync(x => (x.Status == IncidentStatus.Open || x.Status == IncidentStatus.InProgress) && x.Severity == IncidentSeverity.Critical, ct);
        return new DashboardSummaryDto(from, to,
            await db.Clients.CountAsync(ct), await db.Integrations.CountAsync(ct), await db.Integrations.CountAsync(x => x.Status == IntegrationStatus.Active, ct),
            total, succeeded, failed, successRate, open, critical, successRate.HasValue ? (compliant ? 100m : 0m) : null, average, p95);
    }

    public async Task<IReadOnlyList<TimeSeriesPoint>> GetExecutionSeriesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var grouped = await db.Executions.AsNoTracking().Where(x => x.StartedAt >= from && x.StartedAt <= to)
            .GroupBy(x => x.StartedAt.Date)
            .Select(group => new { Date = group.Key, Succeeded = group.Count(x => x.Status == ExecutionStatus.Succeeded), Failed = group.Count(x => x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter) })
            .OrderBy(x => x.Date).ToListAsync(ct);
        return grouped.Select(x => new TimeSeriesPoint(DateOnly.FromDateTime(x.Date), x.Succeeded, x.Failed)).ToList();
    }

    public async Task<IReadOnlyList<MetricValue>> GetIncidentSeverityCountsAsync(CancellationToken ct) =>
        await db.Incidents.AsNoTracking().Where(x => x.Status == IncidentStatus.Open || x.Status == IncidentStatus.InProgress)
            .GroupBy(x => x.Severity).Select(g => new MetricValue(g.Key.ToString(), g.Count())).ToListAsync(ct);

    public async Task<IReadOnlyList<MetricValue>> GetIntegrationStatusCountsAsync(CancellationToken ct) =>
        await db.Integrations.AsNoTracking().GroupBy(x => x.Status).Select(g => new MetricValue(g.Key.ToString(), g.Count())).ToListAsync(ct);

    public async Task<IReadOnlyList<ProviderPerformanceDto>> GetProviderPerformanceAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var raw = await db.Executions.AsNoTracking().Where(x => x.StartedAt >= from && x.StartedAt <= to &&
            (x.Status == ExecutionStatus.Succeeded || x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter))
            .Select(x => new { x.Status, x.DurationMs, ProviderId = x.Integration!.ProviderId, ProviderName = x.Integration.Provider!.Name }).ToListAsync(ct);
        return raw.GroupBy(x => new { x.ProviderId, x.ProviderName }).Select(group =>
        {
            var good = group.Count(x => x.Status == ExecutionStatus.Succeeded);
            var completed = group.Count();
            var durations = group.Where(x => x.DurationMs.HasValue).Select(x => x.DurationMs!.Value).ToArray();
            return new ProviderPerformanceDto(group.Key.ProviderId, group.Key.ProviderName, group.Count(), completed == 0 ? 0m : Math.Round(good * 100m / completed, 2), durations.Length == 0 ? null : (decimal?)durations.Average());
        }).OrderByDescending(x => x.Executions).ToList();
    }

    public async Task<SlaReportDto?> GetIntegrationSlaAsync(Guid integrationId, DateTimeOffset from, DateTimeOffset to, decimal target, int maxAverageMs, CancellationToken ct)
    {
        if (!await db.Integrations.AnyAsync(x => x.Id == integrationId, ct)) return null;
        var query = db.Executions.AsNoTracking().Where(x => x.IntegrationId == integrationId && x.StartedAt >= from && x.StartedAt <= to &&
            (x.Status == ExecutionStatus.Succeeded || x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter));
        var total = await query.CountAsync(ct);
        var good = await query.CountAsync(x => x.Status == ExecutionStatus.Succeeded, ct);
        var failed = await query.CountAsync(x => x.Status == ExecutionStatus.Failed || x.Status == ExecutionStatus.RetryScheduled || x.Status == ExecutionStatus.DeadLetter, ct);
        var durations = await query.Where(x => x.DurationMs != null).Select(x => x.DurationMs!.Value).ToListAsync(ct);
        var successRate = total == 0 ? null : (decimal?)Math.Round(good * 100m / total, 2);
        var average = durations.Count == 0 ? null : (decimal?)durations.Average();
        decimal? compliance = successRate.HasValue && average.HasValue ? (successRate >= target && average <= maxAverageMs ? 100m : 0m) : null;
        return new SlaReportDto(integrationId, from, to, total, good, failed, successRate, average, Percentile95(durations), compliance, target, maxAverageMs);
    }

    private static long? Percentile95(IEnumerable<long> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return null;
        var index = (int)Math.Ceiling(ordered.Length * .95) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }
}
