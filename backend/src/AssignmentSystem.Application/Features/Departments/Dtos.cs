using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Departments;

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Code
);

public sealed record CreateDepartmentCommand(
    string Name,
    string Code
) : ICommand<DepartmentDto>;

public sealed record UpdateDepartmentCommand(
    Guid Id,
    string Name,
    string Code
) : ICommand<DepartmentDto>;

public sealed record DeleteDepartmentCommand(Guid Id) : ICommand;

public sealed record GetDepartmentByIdQuery(Guid Id) : IQuery<DepartmentDto>;

public sealed record GetDepartmentsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<DepartmentDto>>;
