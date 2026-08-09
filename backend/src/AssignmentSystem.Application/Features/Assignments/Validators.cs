using AssignmentSystem.Application.Common.Html;
using FluentValidation;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// The ceiling on a description's markup. It is not a limit on anyone's writing — the editor
/// stops typing at 5000 characters and no amount of formatting inflates that this far — but
/// on what a hand-crafted request may put in the column.
/// </summary>
file static class DescriptionLimits
{
    public const int MaxLength = 20_000;
}

public sealed class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.ClassCourseId)
            .NotEmpty().WithMessage("Choose the class and course this assignment is for.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        // The description arrives as HTML from the rich-text editor, so "required" cannot be a
        // length check: an editor that was typed into and then emptied still posts "<p></p>".
        RuleFor(x => x.Description)
            .Must(description => HtmlContent.HasText(description)).WithMessage("Description is required.")
            .MaximumLength(DescriptionLimits.MaxLength)
            .WithMessage($"Description cannot exceed {DescriptionLimits.MaxLength} characters.");

        RuleFor(x => x.DeadlineUtc)
            .NotEmpty().WithMessage("Deadline is required.");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0).WithMessage("Maximum marks must be greater than zero.");
    }
}

public sealed class UpdateAssignmentCommandValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Assignment id is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        // The description arrives as HTML from the rich-text editor, so "required" cannot be a
        // length check: an editor that was typed into and then emptied still posts "<p></p>".
        RuleFor(x => x.Description)
            .Must(description => HtmlContent.HasText(description)).WithMessage("Description is required.")
            .MaximumLength(DescriptionLimits.MaxLength)
            .WithMessage($"Description cannot exceed {DescriptionLimits.MaxLength} characters.");

        RuleFor(x => x.DeadlineUtc)
            .NotEmpty().WithMessage("Deadline is required.");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0).WithMessage("Maximum marks must be greater than zero.");
    }
}
