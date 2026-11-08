using IntegrationHub.Api.Security;
using IntegrationHub.Application.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/executions/{executionId:guid}/logs")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class ExecutionLogsController(LogQueryUseCases logs) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<IntegrationLogDto>> Search(Guid executionId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        logs.SearchAsync(null, null, null, null, executionId, null, null, null, page, pageSize, cancellationToken);
}
