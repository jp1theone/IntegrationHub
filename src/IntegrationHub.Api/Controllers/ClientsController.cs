using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Clients;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class ClientsController(ClientUseCases clients,
    IValidator<CreateClientRequest> createValidator, IValidator<UpdateClientRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<ClientDto>> Search([FromQuery] string? q, [FromQuery] RecordStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        clients.SearchAsync(q, status, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await clients.GetAsync(id, cancellationToken) is { } client ? Ok(client) : NotFound();

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<ActionResult<ClientDto>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await clients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> Update(Guid id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        return await clients.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> SetStatus(Guid id, SetStatusRequest request, CancellationToken cancellationToken) =>
        await clients.SetActiveAsync(id, request.Active, cancellationToken) ? NoContent() : NotFound();
}

public sealed record SetStatusRequest(bool Active);
