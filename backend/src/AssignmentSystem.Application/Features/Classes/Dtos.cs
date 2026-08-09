using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Classes;

/// <summary>
/// A class cohort as the API returns it: the grade and the section as two fields, never
/// joined into one. Clients that want a label build it themselves; clients that want two
/// dropdowns or two columns — which is all of them — get exactly what they need.
/// </summary>
public sealed record ClassDto(
    Guid Id,
    /// <summary>Grade as a number, 1..12. Shown as the number, not a numeral.</summary>
    int Level,
    string? Section,
    int StudentCount = 0
);

// Create/Update take the grade and the section, which is all a class is.
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
