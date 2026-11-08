using IntegrationHub.Api.Security;
using IntegrationHub.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class DashboardController(ReportingUseCases reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> Summary([FromQuery] int windowDays = 30, CancellationToken cancellationToken = default) => reports.DashboardAsync(windowDays, cancellationToken);
    [HttpGet("executions")]
    public Task<object> Executions([FromQuery] int windowDays = 30, CancellationToken cancellationToken = default) => reports.SeriesAsync(windowDays, cancellationToken);
    [HttpGet("incidents")]
    public Task<IReadOnlyList<MetricValue>> Incidents(CancellationToken cancellationToken) => reports.IncidentSeverityAsync(cancellationToken);
    [HttpGet("integrations")]
    public Task<IReadOnlyList<MetricValue>> Integrations(CancellationToken cancellationToken) => reports.IntegrationStatusAsync(cancellationToken);
    [HttpGet("providers")]
    public Task<IReadOnlyList<ProviderPerformanceDto>> Providers([FromQuery] int windowDays = 30, CancellationToken cancellationToken = default) => reports.ProvidersAsync(windowDays, cancellationToken);
}
