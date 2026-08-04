using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.AssignmentFiles;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// An assignment as every screen reads it. The class and course are flattened out of the
/// offering: the client should not have to know that <c>ClassCourseId</c> is the thing the
/// row actually points at in order to render "Mathematics · Class X - Section A".
/// </summary>
public sealed record AssignmentDto(
    Guid Id,
    Guid ClassCourseId,
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

/// <summary>
/// <paramref name="TeacherId"/> is only read for an admin, who has to say which teacher
/// the work belongs to. A teacher creating their own assignment is always the author, so
/// the value is ignored rather than trusted — otherwise it would be a way to author work
/// under a colleague's name.
/// </summary>
public sealed record CreateAssignmentCommand(
    Guid ClassCourseId,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission,
    Guid? TeacherId = null
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
    Guid? ClassCourseId = null,
    Guid? TeacherId = null,
    AssignmentStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<AssignmentDto>>;
