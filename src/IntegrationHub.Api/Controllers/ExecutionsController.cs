using IntegrationHub.Api.Security;
using IntegrationHub.Application.Executions;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/executions")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class ExecutionsController(IExecutionStore executions, ExecuteIntegrationUseCase processor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IntegrationHub.Application.Common.PagedResult<ExecutionDto>>> Search(
        [FromQuery] string? q, [FromQuery] Guid? integrationId, [FromQuery] ExecutionStatus? status,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var normalized = IntegrationHub.Application.Common.PageLimits.Normalize(page, pageSize);
        if (from.HasValue && to.HasValue && from > to) return BadRequest(new ProblemDetails { Title = "The from date must be earlier than the to date.", Status = 400 });
        return Ok(await executions.SearchAsync(q?.Trim(), integrationId, status, from, to, normalized.Page, normalized.PageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExecutionDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await executions.GetDtoAsync(id, cancellationToken) is { } execution ? Ok(execution) : NotFound();

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = AuthorizationPolicies.RetryExecution)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        await processor.RetryAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id }, new { id, status = "RetryAccepted" });
    }
}
