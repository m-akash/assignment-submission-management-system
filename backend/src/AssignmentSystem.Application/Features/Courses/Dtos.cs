using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Courses;

public sealed record CourseDto(
    Guid Id,
    string Name,
    string Code
);

public sealed record CreateCourseCommand(
    string Name,
    string Code
) : ICommand<CourseDto>;

public sealed record UpdateCourseCommand(
    Guid Id,
    string Name,
    string Code
) : ICommand<CourseDto>;

public sealed record DeleteCourseCommand(Guid Id) : ICommand;

public sealed record GetCourseByIdQuery(Guid Id) : IQuery<CourseDto>;

public sealed record GetCoursesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<CourseDto>>;
