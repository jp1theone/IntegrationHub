using FluentValidation;

namespace IntegrationHub.Application.Providers;

public sealed class CreateProviderValidator : AbstractValidator<CreateProviderRequest>
{
    public CreateProviderValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 300);
        RuleFor(x => x.BaseUrl).Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.BaseUrl)).WithMessage("Base URL must be an absolute HTTP or HTTPS URL.");
    }

    private static bool BeHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public sealed class UpdateProviderValidator : AbstractValidator<UpdateProviderRequest>
{
    public UpdateProviderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TimeoutSeconds).InclusiveBetween(1, 300);
        RuleFor(x => x.BaseUrl).Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.BaseUrl)).WithMessage("Base URL must be an absolute HTTP or HTTPS URL.");
    }

    private static bool BeHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
