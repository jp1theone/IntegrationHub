using System.Security.Claims;
using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Incidents;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class IncidentsController(IncidentUseCases incidents, IValidator<CreateIncidentRequest> createValidator,
    IValidator<AddIncidentCommentRequest> commentValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<IncidentDto>> Search([FromQuery] string? q, [FromQuery] IncidentStatus? status,
        [FromQuery] IncidentSeverity? severity, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        incidents.SearchAsync(q, status, severity, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await incidents.GetAsync(id, cancellationToken) is { } incident ? Ok(incident) : NotFound();

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<ActionResult<IncidentDto>> Create(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await incidents.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Administrator,Integration Analyst,Support Analyst")]
    public async Task<IActionResult> SetStatus(Guid id, SetIncidentStatusRequest request, CancellationToken cancellationToken) =>
        await incidents.UpdateStatusAsync(id, request.Status, cancellationToken) ? NoContent() : NotFound();

    [HttpPatch("{id:guid}/assignment")]
    [Authorize(Roles = "Administrator,Integration Analyst,Support Analyst")]
    public async Task<IActionResult> Assign(Guid id, AssignIncidentRequest request, CancellationToken cancellationToken) =>
        await incidents.AssignAsync(id, request.AssignedToUserId, cancellationToken) ? NoContent() : NotFound();

    [HttpPatch("{id:guid}/severity")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> SetSeverity(Guid id, SetIncidentSeverityRequest request, CancellationToken cancellationToken) =>
        await incidents.SetSeverityAsync(id, request.Severity, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/comments")]
    [Authorize(Roles = "Administrator,Integration Analyst,Support Analyst")]
    public async Task<ActionResult<IncidentDto>> AddComment(Guid id, AddIncidentCommentRequest request, CancellationToken cancellationToken)
    {
        var validation = await commentValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var author = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return await incidents.AddCommentAsync(id, author, request.Content, cancellationToken) is { } updated ? Ok(updated) : NotFound();
    }
}

public sealed record SetIncidentStatusRequest(IncidentStatus Status);
public sealed record AssignIncidentRequest(string? AssignedToUserId);
public sealed record SetIncidentSeverityRequest(IncidentSeverity Severity);
