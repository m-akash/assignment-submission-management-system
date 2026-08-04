using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Groups;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/groups")]
[Authorize]
public sealed class GroupsController : ControllerBase
{
    private readonly ICommandHandler<CreateGroupCommand, GroupDto> _createHandler;
    private readonly ICommandHandler<UpdateGroupCommand, GroupDto> _updateHandler;
    private readonly ICommandHandler<DeleteGroupCommand> _deleteHandler;
    private readonly IQueryHandler<GetGroupByIdQuery, GroupDto> _getByIdHandler;
    private readonly IQueryHandler<GetGroupsQuery, Shared.Common.PageResult<GroupDto>> _getListHandler;

    public GroupsController(
        ICommandHandler<CreateGroupCommand, GroupDto> createHandler,
        ICommandHandler<UpdateGroupCommand, GroupDto> updateHandler,
        ICommandHandler<DeleteGroupCommand> deleteHandler,
        IQueryHandler<GetGroupByIdQuery, GroupDto> getByIdHandler,
        IQueryHandler<GetGroupsQuery, Shared.Common.PageResult<GroupDto>> getListHandler)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _getByIdHandler = getByIdHandler;
        _getListHandler = getListHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetGroups(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _getListHandler.HandleAsync(new GetGroupsQuery(search, page, pageSize), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroupById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(new GetGroupByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(new CreateGroupCommand(request.Name, request.Code), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetGroupById), new { id = result.Value!.Id },
            new ApiResponse<GroupDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
    {
        var result = await _updateHandler.HandleAsync(new UpdateGroupCommand(id, request.Name, request.Code), ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        var result = await _deleteHandler.HandleAsync(new DeleteGroupCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateGroupRequest(string Name, string Code);
public sealed record UpdateGroupRequest(string Name, string Code);

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(150).WithMessage("Group name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Group code is required.")
            .MaximumLength(10).WithMessage("Group code cannot exceed 10 characters.");
    }
}

public sealed class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(150).WithMessage("Group name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Group code is required.")
            .MaximumLength(10).WithMessage("Group code cannot exceed 10 characters.");
    }
}
