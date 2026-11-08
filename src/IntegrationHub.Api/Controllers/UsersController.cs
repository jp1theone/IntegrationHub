using System.Security.Claims;
using FluentValidation;
using IntegrationHub.Api.Security;
using IntegrationHub.Api.Validation;
using IntegrationHub.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.ManageUsers)]
public sealed class UsersController(UserManagementUseCases users, IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public Task<IntegrationHub.Application.Common.PagedResult<UserDto>> Search([FromQuery] string? q, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => users.SearchAsync(q, page, pageSize, cancellationToken);

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> Get(string id, CancellationToken cancellationToken) =>
        await users.GetAsync(id, cancellationToken) is { } user ? Ok(user) : NotFound();

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var created = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        if (id == User.FindFirstValue(ClaimTypes.NameIdentifier)) return Conflict(new ProblemDetails { Title = "You cannot change your own administrative account through this endpoint.", Status = 409 });
        return await users.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> SetStatus(string id, UserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!request.Active && id == User.FindFirstValue(ClaimTypes.NameIdentifier)) return Conflict(new ProblemDetails { Title = "You cannot deactivate your own account.", Status = 409 });
        return await users.SetActiveAsync(id, request.Active, cancellationToken) ? NoContent() : NotFound();
    }
}

public sealed record UserStatusRequest(bool Active);
