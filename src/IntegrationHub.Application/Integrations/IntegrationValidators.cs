using FluentValidation;

namespace IntegrationHub.Application.Integrations;

public sealed class CreateIntegrationValidator : AbstractValidator<CreateIntegrationRequest>
{
    public CreateIntegrationValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(48).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class UpdateIntegrationValidator : AbstractValidator<UpdateIntegrationRequest>
{
    public UpdateIntegrationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
