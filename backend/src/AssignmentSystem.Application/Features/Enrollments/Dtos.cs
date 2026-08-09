using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Enrollments;

/// <summary>One student's membership of one class.</summary>
public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    string? StudentNumber,
    Guid ClassId,
    string ClassName,
    int ClassLevel,
    string? ClassSection,
    DateTime EnrolledAtUtc
);

/// <summary>
/// The compact shape embedded in <c>UserDto.Classes</c> — a student's classes as seen
/// from the student, without repeating the student back at the caller.
/// </summary>
public sealed record EnrolledClassDto(
    Guid EnrollmentId,
    Guid ClassId,
    string ClassName,
    int ClassLevel,
    string? ClassSection,
    DateTime EnrolledAtUtc
);

[RequiresRole(Role.Admin)]
public sealed record CreateEnrollmentCommand(
    Guid StudentId,
    Guid ClassId
) : ICommand<EnrollmentDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteEnrollmentCommand(Guid Id) : ICommand;

[RequiresRole(Role.Admin, Role.Teacher)]
// Multi-valued filters, each bound from its singular query parameter repeated
// (?classId=a&classId=b); empty or absent means "not filtered".
public sealed record GetEnrollmentsQuery(
    IReadOnlyList<Guid>? StudentIds = null,
    IReadOnlyList<Guid>? ClassIds = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<EnrollmentDto>>;
