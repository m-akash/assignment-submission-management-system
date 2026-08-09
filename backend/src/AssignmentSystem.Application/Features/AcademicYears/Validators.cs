using FluentValidation;

namespace AssignmentSystem.Application.Features.AcademicYears;

public sealed class CreateAcademicYearCommandValidator : AbstractValidator<CreateAcademicYearCommand>
{
    public CreateAcademicYearCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Academic year name is required.")
            .MaximumLength(50).WithMessage("Academic year name cannot exceed 50 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate).WithMessage("The end date must be after the start date.");
    }
}

public sealed class UpdateAcademicYearCommandValidator : AbstractValidator<UpdateAcademicYearCommand>
{
    public UpdateAcademicYearCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Academic year id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Academic year name is required.")
            .MaximumLength(50).WithMessage("Academic year name cannot exceed 50 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate).WithMessage("The end date must be after the start date.");
    }
}
