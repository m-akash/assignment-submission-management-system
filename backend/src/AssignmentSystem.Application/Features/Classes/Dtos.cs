using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Classes;

public sealed record ClassDto(
    Guid Id,
    string Name,
    int Level,
    /// <summary>The level as a Roman numeral ("IX") — derived, never stored.</summary>
    string GradeLabel,
    string? Section,
    int StudentCount = 0
);

public sealed record CreateClassCommand(
    string Name,
    int Level,
    string? Section
) : ICommand<ClassDto>;

public sealed record UpdateClassCommand(
    Guid Id,
    string Name,
    int Level,
    string? Section
) : ICommand<ClassDto>;

public sealed record DeleteClassCommand(Guid Id) : ICommand;

public sealed record GetClassByIdQuery(Guid Id) : IQuery<ClassDto>;

public sealed record GetClassesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<ClassDto>>;
