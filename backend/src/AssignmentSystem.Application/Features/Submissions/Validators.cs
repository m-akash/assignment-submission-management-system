using FluentValidation;

namespace AssignmentSystem.Application.Features.Submissions;

// "A submission must include a text answer or a file" is deliberately NOT validated here.
// It depends on which attachments are already stored, which only the handler and the
// domain can see — a request-shape validator would have to trust the client for that.
// Submission.Create / UpdateContent enforce it and surface a 422.

public sealed class SubmitAssignmentCommandValidator : AbstractValidator<SubmitAssignmentCommand>
{
    public SubmitAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId)
            .NotEmpty().WithMessage("Assignment id is required.");
    }
}

public sealed class UpdateSubmissionCommandValidator : AbstractValidator<UpdateSubmissionCommand>
{
    public UpdateSubmissionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Submission id is required.");
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

        RuleFor(x => x.Content)
            .NotNull().WithMessage("File content is required.");
    }
}
