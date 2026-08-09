using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

/// <summary>
/// "This teacher teaches this course to this class." <c>ClassCourseId</c> is the row it
/// actually points at; the class and course are flattened out of the offering so the
/// client can render the mapping without a second lookup.
/// </summary>
public sealed record TeacherAssignmentDto(
    Guid Id,
    Guid TeacherId,
    string TeacherName,
    string TeacherEmail,
    Guid ClassCourseId,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    Guid ClassId,
    string ClassName
);

[RequiresRole(Role.Admin)]
public sealed record CreateTeacherAssignmentCommand(
    Guid TeacherId,
    Guid ClassCourseId
) : ICommand<TeacherAssignmentDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteTeacherAssignmentCommand(Guid Id) : ICommand;

[RequiresRole(Role.Admin, Role.Teacher)]
// Multi-valued filters, each bound from its singular query parameter repeated
// (?classId=a&classId=b); empty or absent means "not filtered".
public sealed record GetTeacherAssignmentsQuery(
    IReadOnlyList<Guid>? TeacherIds = null,
    IReadOnlyList<Guid>? CourseIds = null,
    IReadOnlyList<Guid>? ClassIds = null,
    IReadOnlyList<Guid>? ClassCourseIds = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<TeacherAssignmentDto>>;
