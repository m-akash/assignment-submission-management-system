using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Courses;

public sealed record CourseDto(
    Guid Id,
    string Name,
    string Code,
    Guid DepartmentId,
    string? DepartmentName,
    string? DepartmentCode
);

public sealed record CreateCourseCommand(
    string Name,
    string Code,
    Guid DepartmentId
) : ICommand<CourseDto>;

public sealed record UpdateCourseCommand(
    Guid Id,
    string Name,
    string Code,
    Guid DepartmentId
) : ICommand<CourseDto>;

public sealed record DeleteCourseCommand(Guid Id) : ICommand;

public sealed record GetCourseByIdQuery(Guid Id) : IQuery<CourseDto>;

public sealed record GetCoursesQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<CourseDto>>;
