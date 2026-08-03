using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Submissions;

public sealed record SubmissionFileDto(
    Guid Id,
    Guid SubmissionId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc
);

public sealed record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    string AssignmentTitle,
    Guid StudentId,
    string StudentName,
    string? Content,
    SubmissionStatus Status,
    DateTime? SubmittedAtUtc,
    decimal? Marks,
    decimal? MarksOutOf,
    string? Feedback,
    Guid? ReviewedById,
    string? ReviewedByName,
    DateTime? ReviewedAtUtc,
    List<SubmissionFileDto> Files
);

// Note: neither command takes file ids. Attachments are created by the upload endpoint
// and belong to the submission already; trusting a client-supplied id list would let a
// caller satisfy the "text or file" rule with ids it does not own — or with none at all.
public sealed record SubmitAssignmentCommand(
    Guid AssignmentId,
    string? Content
) : ICommand<SubmissionDto>;

public sealed record UpdateSubmissionCommand(
    Guid Id,
    string? Content
) : ICommand<SubmissionDto>;

public sealed record ReviewSubmissionCommand(
    Guid Id,
    decimal Marks,
    string? Feedback,
    SubmissionStatus Status
) : ICommand<SubmissionDto>;

public sealed record GetSubmissionByIdQuery(Guid Id) : IQuery<SubmissionDto>;

public sealed record GetStudentSubmissionQuery(Guid AssignmentId) : IQuery<SubmissionDto>;

public sealed record GetSubmissionsQuery(
    Guid? AssignmentId = null,
    Guid? StudentId = null,
    SubmissionStatus? Status = null,
    /// <summary>Free-text match on the student's name or the assignment title.</summary>
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<SubmissionDto>>;

/// <summary>
/// An uploaded file as the client presented it. Only <see cref="Content"/> is
/// authoritative — the name is sanitised, the size re-checked while streaming, and the
/// content type re-derived server-side.
/// </summary>
public sealed record UploadSubmissionFileCommand(
    Guid AssignmentId,
    string FileName,
    long SizeBytes,
    Stream Content
) : ICommand<SubmissionFileDto>;

public sealed record DownloadSubmissionFileQuery(Guid FileId) : IQuery<SubmissionFileDownloadResult>;

public sealed record SubmissionFileDownloadResult(Stream Stream, string ContentType, string FileName);

public sealed record DeleteSubmissionFileCommand(Guid FileId) : ICommand;
