using AssignmentSystem.Domain.Common;
using FluentValidation;

namespace AssignmentSystem.Application.Features.Submissions;

// "A submission must include at least one file" is deliberately NOT validated here.
// It depends on which attachments are already stored, which only the handler and the
// domain can see — a request-shape validator would have to trust the client for that.
// Submission.Create / MarkSubmitted enforce it and surface a 422.

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

        // An unmapped enum value arrives as a plain integer cast, so this is the only
        // thing standing between a hand-rolled request and a submission in a status the
        // domain has no meaning for.
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("That is not a valid submission status.");
    }
}

public sealed class RenameSubmissionFileCommandValidator : AbstractValidator<RenameSubmissionFileCommand>
{
    public RenameSubmissionFileCommandValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File id is required.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            // The domain trims this to fit alongside the extension; rejecting outright is
            // clearer than silently returning a name the caller did not ask for.
            .MaximumLength(FileNames.MaxLength).WithMessage("That file name is too long.");
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
