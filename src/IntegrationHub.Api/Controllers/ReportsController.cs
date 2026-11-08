using IntegrationHub.Api.Security;
using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Application.Reporting;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class ReportsController(IExecutionStore executions, IncidentUseCases incidents, ReportingUseCases reports) : ControllerBase
{
    [HttpGet("executions")]
    public Task<IntegrationHub.Application.Common.PagedResult<ExecutionDto>> Executions([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] ExecutionStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        executions.SearchAsync(null, null, status, from, to, page, pageSize, cancellationToken);

    [HttpGet("incidents")]
    public Task<IntegrationHub.Application.Common.PagedResult<IncidentDto>> Incidents([FromQuery] IncidentStatus? status, [FromQuery] IncidentSeverity? severity,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        incidents.SearchAsync(null, status, severity, page, pageSize, cancellationToken);

    [HttpGet("sla")]
    public async Task<ActionResult<SlaReportDto>> Sla([FromQuery] Guid integrationId, [FromQuery] int windowDays = 30, CancellationToken cancellationToken = default) =>
        await reports.IntegrationSlaAsync(integrationId, windowDays, cancellationToken) is { } report ? Ok(report) : NotFound();
}
