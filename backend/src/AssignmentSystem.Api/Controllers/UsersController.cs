using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public UsersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] Role? role,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetUsersQuery(role, classId, search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetUserByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var command = new CreateUserCommand(request.Email, request.FullName, request.Password, request.Role, request.ClassId);
        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetUserById), new { id = result.Value!.Id }, new ApiResponse<UserDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Password);
        var result = await _dispatcher.SendAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteUserCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateUserRequest(string Email, string FullName, string Password, Role Role, Guid? ClassId);
/// <summary>
/// No class here: moving a student between classes goes through the enrollments endpoint,
/// which enforces that they never end up with none.
/// </summary>
public sealed record UpdateUserRequest(string FullName, string? Password);

