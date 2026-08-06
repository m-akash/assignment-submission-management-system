using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.AssignmentFiles;

public sealed record AssignmentFileDto(
    Guid Id,
    Guid AssignmentId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc
);

/// <summary>
/// An uploaded file as the client presented it. Only <see cref="Content"/> is
/// authoritative — the name is sanitised, the size re-checked while streaming, and the
/// content type re-derived server-side.
/// </summary>
[RequiresRole(Role.Teacher)]
public sealed record UploadAssignmentFileCommand(
    Guid AssignmentId,
    string FileName,
    long SizeBytes,
    Stream Content
) : ICommand<AssignmentFileDto>;

[RequiresAuthentication]
public sealed record DownloadAssignmentFileQuery(Guid FileId) : IQuery<AssignmentFileDownloadResult>;

public sealed record AssignmentFileDownloadResult(Stream Stream, string ContentType, string FileName);

[RequiresRole(Role.Teacher)]
public sealed record DeleteAssignmentFileCommand(Guid FileId) : ICommand;
