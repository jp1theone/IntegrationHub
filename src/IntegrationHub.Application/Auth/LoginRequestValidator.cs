using FluentValidation;

namespace IntegrationHub.Application.Auth;

public sealed record LoginCredentials(string Email, string Password);

public sealed class LoginCredentialsValidator : AbstractValidator<LoginCredentials>
{
    public LoginCredentialsValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
