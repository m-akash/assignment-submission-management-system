using AssignmentSystem.Application.Common.Handlers;
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

public sealed record CreateTeacherAssignmentCommand(
    Guid TeacherId,
    Guid ClassCourseId
) : ICommand<TeacherAssignmentDto>;

public sealed record DeleteTeacherAssignmentCommand(Guid Id) : ICommand;

public sealed record GetTeacherAssignmentsQuery(
    Guid? TeacherId = null,
    Guid? CourseId = null,
    Guid? ClassId = null,
    Guid? ClassCourseId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<TeacherAssignmentDto>>;
