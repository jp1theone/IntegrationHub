using System.Security.Claims;
using FluentValidation;
using IntegrationHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using IntegrationHub.Api.Validation;

namespace IntegrationHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationService authentication, IJwtTokenService tokens, IValidator<LoginCredentials> validator) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginCredentials request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(ValidationProblemFactory.From(validation));
        var user = await authentication.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        if (user is null) return Unauthorized(new ProblemDetails { Title = "Credenciales inválidas", Status = StatusCodes.Status401Unauthorized });
        var (token, expiresAt) = tokens.Create(user);
        return Ok(new LoginResponse(token, expiresAt, new CurrentUserResponse(user.Id, user.Email, user.FullName, user.Roles)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null) return Unauthorized();
        var user = await authentication.GetByIdAsync(id, cancellationToken);
        return user is null ? Unauthorized() : Ok(new CurrentUserResponse(user.Id, user.Email, user.FullName, user.Roles));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout() => NoContent();
}

public sealed record CurrentUserResponse(string Id, string Email, string FullName, IReadOnlyCollection<string> Roles);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User);
