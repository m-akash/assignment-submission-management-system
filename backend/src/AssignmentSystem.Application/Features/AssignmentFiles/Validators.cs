using AssignmentSystem.Domain.Common;
using FluentValidation;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

// Uploads are not validated here: what matters about a file is its size, its extension
// and its leading bytes, and only IFileUploadPolicy can see those. A rename is the one
// file operation whose request shape *is* the whole request.

public sealed class RenameAssignmentFileCommandValidator : AbstractValidator<RenameAssignmentFileCommand>
{
    public RenameAssignmentFileCommandValidator()
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
