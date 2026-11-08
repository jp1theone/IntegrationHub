using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Providers;
using IntegrationHub.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/providers")]
[Authorize(Policy = AuthorizationPolicies.ViewOperations)]
public sealed class ProvidersController(ProviderUseCases providers,
    IValidator<CreateProviderRequest> createValidator, IValidator<UpdateProviderRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<ProviderDto>> Search([FromQuery] string? q, [FromQuery] RecordStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        providers.SearchAsync(q, status, page, pageSize, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProviderDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await providers.GetAsync(id, cancellationToken) is { } provider ? Ok(provider) : NotFound();

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<ActionResult<ProviderDto>> Create(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await providers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> Update(Guid id, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        return await providers.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ManageIntegrations)]
    public async Task<IActionResult> SetStatus(Guid id, SetStatusRequest request, CancellationToken cancellationToken) =>
        await providers.SetActiveAsync(id, request.Active, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/health")]
    public async Task<IActionResult> Health(Guid id, CancellationToken cancellationToken) =>
        await providers.HealthAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
}
