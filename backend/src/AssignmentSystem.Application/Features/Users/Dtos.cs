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
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    Domain.Enums.Role Role,
    Guid? ClassId
) : ICommand<UserDto>;

/// <summary>
/// Profile and password only. Moving a student between classes is deliberately not here:
/// enrollments are their own resource with their own rules (you cannot remove a student's
/// last class), and folding them into a profile update would let a single PUT quietly
/// bypass those.
/// </summary>
public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? Password
) : ICommand<UserDto>;

public sealed record DeleteUserCommand(Guid Id) : ICommand;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;

/// <summary>
/// Resolves the caller's own profile from the authenticated principal — the identity
/// the frontend renders (including the enrolled <c>Classes</c>, which the login
/// response deliberately omits). Takes no parameters: the id comes from the token.
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<UserDto>;

// Qualified: this record's own `Role` parameter shadows the enum inside its attribute list.
public sealed record GetUsersQuery(
    Domain.Enums.Role? Role = null,
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<UserDto>>;
