using AssignmentSystem.Application.Common.Authorization;
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
/// A teacher creating an assignment is always its author — the identity is taken from the
/// token rather than the request, so a colleague's id can never be named.
/// </summary>
[RequiresRole(Role.Teacher)]
public sealed record CreateAssignmentCommand(
    Guid ClassCourseId,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission
) : ICommand<AssignmentDto>;

[RequiresRole(Role.Teacher)]
public sealed record UpdateAssignmentCommand(
    Guid Id,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission
) : ICommand<AssignmentDto>;

[RequiresRole(Role.Teacher)]
public sealed record DeleteAssignmentCommand(Guid Id) : ICommand;

[RequiresRole(Role.Teacher)]
public sealed record PublishAssignmentCommand(Guid Id) : ICommand<AssignmentDto>;

[RequiresAuthentication]
public sealed record GetAssignmentByIdQuery(Guid Id) : IQuery<AssignmentDto>;

[RequiresAuthentication]
public sealed record GetAssignmentsQuery(
    Guid? ClassId = null,
    Guid? CourseId = null,
    Guid? ClassCourseId = null,
    Guid? TeacherId = null,
    AssignmentStatus? Status = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<AssignmentDto>>;
