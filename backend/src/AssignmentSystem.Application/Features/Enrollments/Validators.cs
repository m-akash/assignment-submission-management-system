using FluentValidation;

namespace AssignmentSystem.Application.Features.Enrollments;

public sealed class CreateEnrollmentCommandValidator : AbstractValidator<CreateEnrollmentCommand>
{
    public CreateEnrollmentCommandValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student id is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class id is required.");

        RuleFor(x => x.AcademicYearId)
            .NotEmpty().WithMessage("Academic year id is required.");
    }
}
