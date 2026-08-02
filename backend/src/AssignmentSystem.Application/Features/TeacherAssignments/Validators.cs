using FluentValidation;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

public sealed class CreateTeacherAssignmentCommandValidator : AbstractValidator<CreateTeacherAssignmentCommand>
{
    public CreateTeacherAssignmentCommandValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher id is required.");

        RuleFor(x => x.SubjectId)
            .NotEmpty().WithMessage("Subject id is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class id is required.");
    }
}
