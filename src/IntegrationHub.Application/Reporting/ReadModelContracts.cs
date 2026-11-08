using IntegrationHub.Domain.Common;

namespace IntegrationHub.Application.Reporting;

public sealed record MetricValue(string Name, decimal Value);
public sealed record TimeSeriesPoint(DateOnly Date, int Succeeded, int Failed);
public sealed record ProviderPerformanceDto(Guid ProviderId, string ProviderName, int Executions, decimal SuccessRate, decimal? AverageDurationMs);
public sealed record DashboardSummaryDto(DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int TotalClients, int TotalIntegrations, int ActiveIntegrations, int TotalExecutions, int SucceededExecutions, int FailedExecutions, decimal? SuccessRatePercent, int OpenIncidents, int CriticalIncidents, decimal? SlaCompliancePercent, decimal? AverageDurationMs, long? P95DurationMs);
public sealed record SlaReportDto(Guid IntegrationId, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, int TotalExecutions, int SucceededExecutions, int FailedExecutions, decimal? SuccessRatePercent, decimal? AverageDurationMs, long? P95DurationMs, decimal? SlaCompliancePercent, decimal AvailabilityTargetPercent, int MaximumAverageDurationMs);
public sealed record SlaPolicyOptions(int DefaultWindowDays, decimal AvailabilityTargetPercent, int MaximumAverageDurationMs);

public interface IReportingStore
{
    Task<DashboardSummaryDto> GetDashboardAsync(DateTimeOffset from, DateTimeOffset to, decimal availabilityTarget, int maxAverageMs, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimeSeriesPoint>> GetExecutionSeriesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetricValue>> GetIncidentSeverityCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MetricValue>> GetIntegrationStatusCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderPerformanceDto>> GetProviderPerformanceAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<SlaReportDto?> GetIntegrationSlaAsync(Guid integrationId, DateTimeOffset from, DateTimeOffset to, decimal availabilityTarget, int maxAverageMs, CancellationToken cancellationToken);
}

public sealed class ReportingUseCases(IReportingStore reports, SlaPolicyOptions options)
{
    public Task<DashboardSummaryDto> DashboardAsync(int windowDays, CancellationToken ct)
    {
        var days = Math.Clamp(windowDays, 1, 365); var end = DateTimeOffset.UtcNow; var start = end.AddDays(-days);
        return reports.GetDashboardAsync(start, end, options.AvailabilityTargetPercent, options.MaximumAverageDurationMs, ct);
    }
    public async Task<object> SeriesAsync(int windowDays, CancellationToken ct)
    {
        var days = Math.Clamp(windowDays, 1, 365); var end = DateTimeOffset.UtcNow; var start = end.AddDays(-days);
        return new { windowStart = start, windowEnd = end, points = await reports.GetExecutionSeriesAsync(start, end, ct) };
    }
    public Task<IReadOnlyList<MetricValue>> IncidentSeverityAsync(CancellationToken ct) => reports.GetIncidentSeverityCountsAsync(ct);
    public Task<IReadOnlyList<MetricValue>> IntegrationStatusAsync(CancellationToken ct) => reports.GetIntegrationStatusCountsAsync(ct);
    public Task<IReadOnlyList<ProviderPerformanceDto>> ProvidersAsync(int windowDays, CancellationToken ct)
    {
        var days = Math.Clamp(windowDays, 1, 365); var end = DateTimeOffset.UtcNow;
        return reports.GetProviderPerformanceAsync(end.AddDays(-days), end, ct);
    }
    public Task<SlaReportDto?> IntegrationSlaAsync(Guid integrationId, int windowDays, CancellationToken ct)
    {
        var days = Math.Clamp(windowDays, 1, 365); var end = DateTimeOffset.UtcNow;
        return reports.GetIntegrationSlaAsync(integrationId, end.AddDays(-days), end,
            options.AvailabilityTargetPercent, options.MaximumAverageDurationMs, ct);
    }
}
