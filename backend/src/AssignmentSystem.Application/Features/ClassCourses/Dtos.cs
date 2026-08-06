using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.ClassCourses;

/// <summary>
/// A course offering. Flattens the class and course names in rather than nesting them:
/// every screen that shows an offering shows "Mathematics · Class X - Section A", so a
/// nested shape would only mean the client re-joining what the server already knows.
/// </summary>
public sealed record ClassCourseDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    int ClassLevel,
    string? ClassSection,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    /// <summary>How many teachers are mapped to this offering — 0 means nobody can set work for it yet.</summary>
    int TeacherCount = 0,
    /// <summary>Assignments created against this offering, draft included.</summary>
    int AssignmentCount = 0
);

[RequiresRole(Role.Admin)]
public sealed record CreateClassCourseCommand(
    Guid ClassId,
    Guid CourseId
) : ICommand<ClassCourseDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteClassCourseCommand(Guid Id) : ICommand;

[RequiresRole(Role.Admin, Role.Teacher)]
public sealed record GetClassCourseByIdQuery(Guid Id) : IQuery<ClassCourseDto>;

[RequiresRole(Role.Admin, Role.Teacher)]
public sealed record GetClassCoursesQuery(
    Guid? ClassId = null,
    Guid? CourseId = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<ClassCourseDto>>;
