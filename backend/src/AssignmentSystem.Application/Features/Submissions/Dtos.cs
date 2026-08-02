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

public sealed record SubmitAssignmentCommand(
    Guid AssignmentId,
    string? Content,
    List<Guid>? FileIds = null
) : ICommand<SubmissionDto>;

public sealed record UpdateSubmissionCommand(
    Guid Id,
    string? Content,
    List<Guid>? FileIds = null
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
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<SubmissionDto>>;

public sealed record UploadSubmissionFileCommand(
    Guid AssignmentId,
    string FileName,
    string ContentType,
    Stream Content
) : ICommand<SubmissionFileDto>;

public sealed record DownloadSubmissionFileQuery(Guid FileId) : IQuery<SubmissionFileDownloadResult>;

public sealed record SubmissionFileDownloadResult(Stream Stream, string ContentType, string FileName);

public sealed record DeleteSubmissionFileCommand(Guid FileId) : ICommand;
