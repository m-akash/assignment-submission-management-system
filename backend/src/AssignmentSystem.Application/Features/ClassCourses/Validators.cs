using FluentValidation;

namespace AssignmentSystem.Application.Features.ClassCourses;

public sealed class CreateClassCourseCommandValidator : AbstractValidator<CreateClassCourseCommand>
{
    public CreateClassCourseCommandValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class id is required.");

        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Course id is required.");
    }
}
