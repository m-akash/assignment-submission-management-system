using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Classes;

public sealed record ClassDto(
    Guid Id,
    /// <summary>"Class IX - Section A" — composed from the grade and section, never entered.</summary>
    string Name,
    int Level,
    /// <summary>The level as a Roman numeral ("IX") — derived, never stored.</summary>
    string GradeLabel,
    string? Section,
    int StudentCount = 0
);

// Create/Update take only the grade and section — the name is composed by the domain, so
// there is nothing for an admin to type and nothing that can disagree with the pair.
[RequiresRole(Role.Admin)]
public sealed record CreateClassCommand(
    int Level,
    string Section
) : ICommand<ClassDto>;

[RequiresRole(Role.Admin)]
public sealed record UpdateClassCommand(
    Guid Id,
    int Level,
    string Section
) : ICommand<ClassDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteClassCommand(Guid Id) : ICommand;

[RequiresAuthentication]
public sealed record GetClassByIdQuery(Guid Id) : IQuery<ClassDto>;

[RequiresAuthentication]
public sealed record GetClassesQuery(
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<ClassDto>>;
