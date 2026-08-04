using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.AssignmentFiles;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Assignments;

public sealed record AssignmentDto(
    Guid Id,
    Guid TeacherAssignmentId,
    Guid TeacherId,
    string TeacherName,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    Guid ClassId,
    string ClassName,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    AssignmentStatus Status,
    bool AllowResubmission,
    int SubmissionCount,
    DateTime CreatedAtUtc,
    List<AssignmentFileDto> Files
);

public sealed record CreateAssignmentCommand(
    Guid TeacherAssignmentId,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission
) : ICommand<AssignmentDto>;

public sealed record UpdateAssignmentCommand(
    Guid Id,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission
) : ICommand<AssignmentDto>;

public sealed record DeleteAssignmentCommand(Guid Id) : ICommand;

public sealed record PublishAssignmentCommand(Guid Id) : ICommand<AssignmentDto>;

public sealed record GetAssignmentByIdQuery(Guid Id) : IQuery<AssignmentDto>;

public sealed record GetAssignmentsQuery(
    Guid? ClassId = null,
    Guid? CourseId = null,
    Guid? TeacherId = null,
    AssignmentStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<AssignmentDto>>;
