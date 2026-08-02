using FluentValidation;

namespace AssignmentSystem.Application.Features.Submissions;

public sealed class SubmitAssignmentCommandValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment id is required.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || (x.FileIds != null && x.FileIds.Count > 0))
            .WithMessage("A submission must include a text answer or at least one file attachment.");
    }
}

public sealed class UpdateSubmissionCommandValidator : AbstractValidator<UpdateSubmissionCommand>
{
    public UpdateSubmissionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Submission id is required.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || (x.FileIds != null && x.FileIds.Count > 0))
            .WithMessage("A submission must include a text answer or at least one file attachment.");
    }
}

public sealed class ReviewSubmissionCommandValidator : AbstractValidator<ReviewSubmissionCommand>
{
    public ReviewSubmissionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Submission id is required.");

        RuleFor(x => x.Marks)
            .GreaterThanOrEqualTo(0).WithMessage("Marks cannot be negative.");
    }
}

public sealed class UploadSubmissionFileCommandValidator : AbstractValidator<UploadSubmissionFileCommand>
{
    public UploadSubmissionFileCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment id is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("File content is required.");
    }
}
