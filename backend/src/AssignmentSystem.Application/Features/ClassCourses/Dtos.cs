using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.ClassCourses;

/// <summary>
/// A course offering. Flattens the class and course in rather than nesting them: every
/// screen that shows an offering shows the course beside the class, so a nested shape would
/// only mean the client re-joining what the server already knows. The class comes through as
/// a grade and a section, two fields, never one joined string.
/// </summary>
public sealed record ClassCourseDto(
    Guid Id,
    Guid ClassId,
    int ClassLevel,
    string? ClassSection,
    Guid CourseId,
    string CourseName,
    string CourseCode,
    /// <summary>How many teachers are mapped to this offering — 0 means nobody can set work for it yet.</summary>
    int TeacherCount = 0,
    /// <summary>Assignments created against this offering, draft included.</summary>
    int AssignmentCount = 0,
    /// <summary>
    /// The names of the teachers mapped to this offering, in name order — empty when nobody
    /// is assigned yet. Sent alongside the count because every screen listing offerings shows
    /// who teaches each one, and a bare number would only make the client fetch the mappings.
    /// </summary>
    IReadOnlyList<string>? TeacherNames = null
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
// Multi-valued filters, each bound from its singular query parameter repeated
// (?classId=a&classId=b); empty or absent means "not filtered".
public sealed record GetClassCoursesQuery(
    IReadOnlyList<Guid>? ClassIds = null,
    IReadOnlyList<Guid>? CourseIds = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<ClassCourseDto>>;
