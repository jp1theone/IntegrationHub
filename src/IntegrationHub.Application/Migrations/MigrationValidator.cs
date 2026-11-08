using FluentValidation;

namespace IntegrationHub.Application.Migrations;

public sealed class CreateMigrationValidator : AbstractValidator<CreateMigrationRequest>
{
    public CreateMigrationValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.IntegrationId).NotEmpty();
        RuleFor(x => x.SourceProvider).NotEmpty().MaximumLength(160);
        RuleFor(x => x.TargetProvider).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Notes).MaximumLength(4000);
    }
}

public sealed class UpdateMigrationValidator : AbstractValidator<UpdateMigrationRequest>
{
    public UpdateMigrationValidator() { RuleFor(x => x.AssignedToUserId).MaximumLength(450); RuleFor(x => x.Notes).MaximumLength(4000); }
}
