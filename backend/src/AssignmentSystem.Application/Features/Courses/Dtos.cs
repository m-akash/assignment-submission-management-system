using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Courses;

public sealed record CourseDto(
    Guid Id,
    string Name,
    string Code
);

[RequiresRole(Role.Admin)]
public sealed record CreateCourseCommand(
    string Name,
    string Code
) : ICommand<CourseDto>;

[RequiresRole(Role.Admin)]
public sealed record UpdateCourseCommand(
    Guid Id,
    string Name,
    string Code
) : ICommand<CourseDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteCourseCommand(Guid Id) : ICommand;

[RequiresAuthentication]
public sealed record GetCourseByIdQuery(Guid Id) : IQuery<CourseDto>;

[RequiresAuthentication]
public sealed record GetCoursesQuery(
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<CourseDto>>;
