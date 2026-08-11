using AssignmentSystem.Application.Common.Authorization;
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

// Note: neither command carries anything but an id. A submission is its attachments,
// which the upload endpoint has already stored against it; trusting a client-supplied
// file id list would let a caller hand in files it does not own — or none at all.
[RequiresRole(Role.Student)]
public sealed record SubmitAssignmentCommand(
    Guid AssignmentId
) : ICommand<SubmissionDto>;

[RequiresRole(Role.Student)]
public sealed record UpdateSubmissionCommand(
    Guid Id
) : ICommand<SubmissionDto>;

[RequiresRole(Role.Teacher)]
public sealed record ReviewSubmissionCommand(
    Guid Id,
    decimal Marks,
    string? Feedback,
    SubmissionStatus Status
) : ICommand<SubmissionDto>;

[RequiresAuthentication]
public sealed record GetSubmissionByIdQuery(Guid Id) : IQuery<SubmissionDto>;

[RequiresRole(Role.Student)]
public sealed record GetStudentSubmissionQuery(Guid AssignmentId) : IQuery<SubmissionDto>;

[RequiresAuthentication]
// Multi-valued filters, each bound from its singular query parameter repeated
// (?status=Pending&status=Late); empty or absent means "not filtered".
public sealed record GetSubmissionsQuery(
    IReadOnlyList<Guid>? AssignmentIds = null,
    /// <summary>Classes the work was set for, reached through the assignment's offering.</summary>
    IReadOnlyList<Guid>? ClassIds = null,
    /// <summary>Courses the work was set for, reached the same way.</summary>
    IReadOnlyList<Guid>? CourseIds = null,
    IReadOnlyList<Guid>? StudentIds = null,
    IReadOnlyList<SubmissionStatus>? Statuses = null,
    /// <summary>Free-text match on the student's name or the assignment title.</summary>
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<SubmissionDto>>;

/// <summary>
/// An uploaded file as the client presented it. Only <see cref="Content"/> is
/// authoritative — the name is sanitised, the size re-checked while streaming, and the
/// content type re-derived server-side.
/// </summary>
[RequiresRole(Role.Student)]
public sealed record UploadSubmissionFileCommand(
    Guid AssignmentId,
    string FileName,
    long SizeBytes,
    Stream Content
) : ICommand<SubmissionFileDto>;

[RequiresAuthentication]
public sealed record DownloadSubmissionFileQuery(Guid FileId) : IQuery<SubmissionFileDownloadResult>;

public sealed record SubmissionFileDownloadResult(Stream Stream, string ContentType, string FileName);

[RequiresRole(Role.Student)]
public sealed record DeleteSubmissionFileCommand(Guid FileId) : ICommand;

/// <summary>
/// Relabels an attachment. <see cref="FileName"/> is a request, not a fact: it is
/// sanitised, and the extension of the stored file is re-applied whatever the caller
/// sends — the bytes were validated against that extension at upload, and a rename is
/// not an opportunity to re-describe them.
/// </summary>
[RequiresRole(Role.Student)]
public sealed record RenameSubmissionFileCommand(Guid FileId, string FileName) : ICommand<SubmissionFileDto>;
