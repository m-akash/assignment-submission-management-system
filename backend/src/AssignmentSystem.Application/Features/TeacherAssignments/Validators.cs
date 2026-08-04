using FluentValidation;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

public sealed class CreateTeacherAssignmentCommandValidator : AbstractValidator<CreateTeacherAssignmentCommand>
{
    public CreateTeacherAssignmentCommandValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher id is required.");

        RuleFor(x => x.ClassCourseId)
            .NotEmpty().WithMessage("Choose the class and course to assign the teacher to.");
    }
}
