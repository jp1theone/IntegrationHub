using IntegrationHub.Api.Security;
using IntegrationHub.Application.Observability;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class LogsController(LogQueryUseCases logs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IntegrationHub.Application.Common.PagedResult<IntegrationLogDto>>> Search(
        [FromQuery] string? q, [FromQuery] Guid? correlationId, [FromQuery] Guid? integrationId, [FromQuery] Guid? providerId,
        [FromQuery] OperationalLogLevel? level, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from > to) return BadRequest(new ProblemDetails { Title = "The from date must be earlier than the to date.", Status = 400 });
        return Ok(await logs.SearchAsync(q, correlationId, integrationId, providerId, null, level, from, to, page, pageSize, cancellationToken));
    }
}
