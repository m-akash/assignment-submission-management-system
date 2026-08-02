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
    bool IsActive,
    DateTime CreatedAtUtc
);

public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    Domain.Enums.Role Role,
    Guid? ClassId
) : ICommand<UserDto>;

public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? Password,
    Guid? ClassId
) : ICommand<UserDto>;

public sealed record DeleteUserCommand(Guid Id) : ICommand;

public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDto>;

public sealed record GetUsersQuery(
    Domain.Enums.Role? Role = null,
    Guid? ClassId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<UserDto>>;
