using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Executions;
using IntegrationHub.Application.Integrations;
using IntegrationHub.Application.Reporting;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class IntegrationsController(IntegrationUseCases integrations, ExecuteIntegrationUseCase execution, IExecutionStore executions, ReportingUseCases reports,
    IValidator<CreateIntegrationRequest> createValidator, IValidator<UpdateIntegrationRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<IntegrationDto>> Search([FromQuery] string? q, [FromQuery] Guid? clientId,
        [FromQuery] Guid? providerId, [FromQuery] IntegrationStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => integrations.SearchAsync(q, clientId, providerId, status, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IntegrationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await integrations.GetAsync(id, cancellationToken) is { } integration ? Ok(integration) : NotFound();

    [HttpGet("{id:guid}/executions")]
    public async Task<IActionResult> Executions(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (await integrations.GetAsync(id, cancellationToken) is null) return NotFound();
        var normalized = IntegrationHub.Application.Common.PageLimits.Normalize(page, pageSize);
        return Ok(await executions.SearchAsync(null, id, null, null, null, normalized.Page, normalized.PageSize, cancellationToken));
    }

    [HttpGet("{id:guid}/sla")]
    public async Task<IActionResult> Sla(Guid id, [FromQuery] int windowDays = 30, CancellationToken cancellationToken = default) =>
        await reports.IntegrationSlaAsync(id, windowDays, cancellationToken) is { } report ? Ok(report) : NotFound();

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<ActionResult<IntegrationDto>> Create(CreateIntegrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await integrations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> Update(Guid id, UpdateIntegrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        return await integrations.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> SetStatus(Guid id, SetIntegrationStatusRequest request, CancellationToken cancellationToken) =>
        await integrations.SetStatusAsync(id, request.Status, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/execute")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    [RequestSizeLimit(256_000)]
    public async Task<IActionResult> Execute(Guid id, ExecuteRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (request.Payload.ValueKind != JsonValueKind.Object) return BadRequest(new ProblemDetails { Title = "Payload must be a JSON object.", Status = StatusCodes.Status400BadRequest });
        if (idempotencyKey?.Length > 200) return BadRequest(new ProblemDetails { Title = "Idempotency-Key cannot exceed 200 characters.", Status = StatusCodes.Status400BadRequest });
        var result = await execution.ExecuteAsync(id, new ExecutionRequest(request.Payload.GetRawText(), idempotencyKey, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value), cancellationToken);
        return result.Status switch
        {
            ExecutionStatus.Succeeded => Ok(result),
            ExecutionStatus.RetryScheduled => Accepted(result),
            _ => StatusCode(StatusCodes.Status502BadGateway, result)
        };
    }
}

public sealed record SetIntegrationStatusRequest(IntegrationStatus Status);
public sealed record ExecuteRequest(JsonElement Payload);
