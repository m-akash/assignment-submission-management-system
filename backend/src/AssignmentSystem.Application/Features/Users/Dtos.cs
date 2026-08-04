using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    Domain.Enums.Role Role,
    Guid? ClassId,
    string? ClassName,
    string? StudentId,
    Guid? GroupId,
    string? GroupName,
    Guid? DepartmentId,
    string? DepartmentName,
    string? TeacherId,
    bool IsActive,
    DateTime CreatedAtUtc
);

public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    Domain.Enums.Role Role,
    Guid? ClassId,
    Guid? DepartmentId,
    Guid? GroupId
) : ICommand<UserDto>;

public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? Password,
    Guid? ClassId,
    Guid? DepartmentId,
    Guid? GroupId
) : ICommand<UserDto>;

public sealed record DeleteUserCommand(Guid Id) : ICommand;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;

/// <summary>
/// Resolves the caller's own profile from the authenticated principal — the identity
/// the frontend renders (including <c>ClassId</c>/<c>ClassName</c>, which the login
/// response deliberately omits). Takes no parameters: the id comes from the token.
/// </summary>
public sealed record GetCurrentUserQuery : IQuery<UserDto>;

public sealed record GetUsersQuery(
    Domain.Enums.Role? Role = null,
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<UserDto>>;
