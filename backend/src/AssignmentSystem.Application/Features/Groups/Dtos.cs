using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Groups;

public sealed record GroupDto(
    Guid Id,
    string Name,
    string Code
);

public sealed record CreateGroupCommand(
    string Name,
    string Code
) : ICommand<GroupDto>;

public sealed record UpdateGroupCommand(
    Guid Id,
    string Name,
    string Code
) : ICommand<GroupDto>;

public sealed record DeleteGroupCommand(Guid Id) : ICommand;

public sealed record GetGroupByIdQuery(Guid Id) : IQuery<GroupDto>;

public sealed record GetGroupsQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<GroupDto>>;
