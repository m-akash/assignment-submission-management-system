using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Application.Features.Enrollments;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Users;

/// <summary>
/// A user. <see cref="Classes"/> is a list because class membership is an enrollment
/// relationship, not a column — empty for admins and teachers, and normally one entry for
/// a student.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    Domain.Enums.Role Role,
    string? StudentId,
    string? TeacherId,
    bool IsActive,
    DateTime CreatedAtUtc,
    List<EnrolledClassDto> Classes
);

/// <summary>
/// <paramref name="ClassId"/> is the class the student is enrolled into on creation — the
/// handler writes the user and that first enrollment in one transaction, so a student never
/// exists in a state where they belong to no class. Further classes are added through the
/// enrollments endpoint.
///
/// <paramref name="AcademicYearId"/> is the session that enrollment belongs to. Optional
/// because the answer is nearly always "the one the school is running": left unset, the
/// handler uses the year flagged as current, and only refuses if there is no current year
/// to fall back on. A caller enrolling into next year's intake names it explicitly.
/// </summary>
[RequiresRole(Role.Admin)]
public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    Domain.Enums.Role Role,
    Guid? ClassId,
    Guid? AcademicYearId = null
) : ICommand<UserDto>;

/// <summary>
/// Profile and password only. Moving a student between classes is deliberately not here:
/// enrollments are their own resource with their own rules (you cannot remove a student's
/// last class), and folding them into a profile update would let a single PUT quietly
/// bypass those.
/// </summary>
[RequiresRole(Role.Admin)]
public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? Password
) : ICommand<UserDto>;

[RequiresRole(Role.Admin)]
public sealed record DeleteUserCommand(Guid Id) : ICommand;

[RequiresRole(Role.Admin)]
public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;

/// <summary>
/// Resolves the caller's own profile from the authenticated principal — the identity
/// the frontend renders (including the enrolled <c>Classes</c>, which the login
/// response deliberately omits). Takes no parameters: the id comes from the token.
/// </summary>
[RequiresAuthentication]
public sealed record GetCurrentUserQuery : IQuery<UserDto>;

// Qualified: this record's own `Role` parameter shadows the enum inside its attribute list.
[RequiresRole(Domain.Enums.Role.Admin)]
public sealed record GetUsersQuery(
    /// <summary>Any of these roles; empty or absent means every role. Bound from a repeated `role` parameter.</summary>
    IReadOnlyList<Domain.Enums.Role>? Roles = null,
    /// <summary>Any of these classes; empty or absent means every class. Bound from a repeated `classId` parameter.</summary>
    IReadOnlyList<Guid>? ClassIds = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<UserDto>>;
