using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.AcademicYears;

public sealed record AcademicYearDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent,
    /// <summary>How many enrollments name this year — what the delete guard reports on.
    /// Defaulted so it stays out of the mapper's constructor call and is filled in by the
    /// handler, the same way <c>ClassDto.StudentCount</c> is.</summary>
    int EnrollmentCount = 0
);

[RequiresRole(Role.Admin)]
public sealed record CreateAcademicYearCommand(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent = false
) : ICommand<AcademicYearDto>;

[RequiresRole(Role.Admin)]
public sealed record UpdateAcademicYearCommand(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent = false
) : ICommand<AcademicYearDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteAcademicYearCommand(Guid Id) : ICommand;

/// <summary>
/// Readable by any signed-in user, like courses: a student's own class list names the year
/// it belongs to, so the label has to be reachable without being an admin.
/// </summary>
[RequiresAuthentication]
public sealed record GetAcademicYearByIdQuery(Guid Id) : IQuery<AcademicYearDto>;

[RequiresAuthentication]
public sealed record GetAcademicYearsQuery(
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<AcademicYearDto>>;
