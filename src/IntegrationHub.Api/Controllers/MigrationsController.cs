using System.Security.Claims;
using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Migrations;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/migrations")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class MigrationsController(MigrationUseCases migrations, IValidator<CreateMigrationRequest> createValidator,
    IValidator<UpdateMigrationRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<MigrationDto>> Search([FromQuery] string? q, [FromQuery] MigrationStatus? status,
        [FromQuery] Guid? clientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        migrations.SearchAsync(q, status, clientId, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MigrationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await migrations.GetAsync(id, cancellationToken) is { } migration ? Ok(migration) : NotFound();

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<ActionResult<MigrationDto>> Create(CreateMigrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await migrations.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> Update(Guid id, UpdateMigrationRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        return await migrations.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> SetStatus(Guid id, SetMigrationStatusRequest request, CancellationToken cancellationToken) =>
        await migrations.ChangeStatusAsync(id, request.Status, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken) ? NoContent() : NotFound();
}

public sealed record SetMigrationStatusRequest(MigrationStatus Status);
