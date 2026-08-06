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

public sealed record CreateEnrollmentCommand(
    Guid StudentId,
    Guid ClassId
) : ICommand<EnrollmentDto>;

public sealed record DeleteEnrollmentCommand(Guid Id) : ICommand;

public sealed record GetEnrollmentsQuery(
    Guid? StudentId = null,
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<EnrollmentDto>>;
