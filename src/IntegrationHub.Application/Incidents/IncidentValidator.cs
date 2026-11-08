using FluentValidation;

namespace IntegrationHub.Application.Incidents;

public sealed class CreateIncidentValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentValidator()
    {
        RuleFor(x => x.IntegrationId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ErrorCode).MaximumLength(80);
    }
}

public sealed record AddIncidentCommentRequest(string Content);
public sealed class AddIncidentCommentValidator : AbstractValidator<AddIncidentCommentRequest>
{
    public AddIncidentCommentValidator() => RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
}
