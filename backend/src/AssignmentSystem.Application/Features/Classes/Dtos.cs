using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Classes;

public sealed record ClassDto(
    Guid Id,
    string Name,
    string? Grade,
    string? Section
);

public sealed record CreateClassCommand(
    string Name,
    string? Grade,
    string? Section
) : ICommand<ClassDto>;

public sealed record UpdateClassCommand(
    Guid Id,
    string Name,
    string? Grade,
    string? Section
) : ICommand<ClassDto>;

public sealed record DeleteClassCommand(Guid Id) : ICommand;

public sealed record GetClassByIdQuery(Guid Id) : IQuery<ClassDto>;

public sealed record GetClassesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<ClassDto>>;
