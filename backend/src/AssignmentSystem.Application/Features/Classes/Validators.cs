using FluentValidation;

namespace AssignmentSystem.Application.Features.Classes;

public sealed class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(150).WithMessage("Class name cannot exceed 150 characters.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 12).WithMessage("Class level must be between 1 and 12.");

        RuleFor(x => x.Section)
            .MaximumLength(50).WithMessage("Section cannot exceed 50 characters.");
    }
}

public sealed class UpdateClassCommandValidator : AbstractValidator<UpdateClassCommand>
{
    public UpdateClassCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Class id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(150).WithMessage("Class name cannot exceed 150 characters.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 12).WithMessage("Class level must be between 1 and 12.");

        RuleFor(x => x.Section)
            .MaximumLength(50).WithMessage("Section cannot exceed 50 characters.");
    }
}
