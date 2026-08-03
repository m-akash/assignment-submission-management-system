using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Users;
using AssignmentSystem.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController : ControllerBase
{
    private readonly ICommandHandler<CreateUserCommand, UserDto> _createUserHandler;
    private readonly ICommandHandler<UpdateUserCommand, UserDto> _updateUserHandler;
    private readonly ICommandHandler<DeleteUserCommand> _deleteUserHandler;
    private readonly IQueryHandler<GetUserByIdQuery, UserDto> _getUserByIdHandler;
    private readonly IQueryHandler<GetUsersQuery, Shared.Common.PageResult<UserDto>> _getUsersHandler;

    public UsersController(
        ICommandHandler<CreateUserCommand, UserDto> createUserHandler,
        ICommandHandler<UpdateUserCommand, UserDto> updateUserHandler,
        ICommandHandler<DeleteUserCommand> deleteUserHandler,
        IQueryHandler<GetUserByIdQuery, UserDto> getUserByIdHandler,
        IQueryHandler<GetUsersQuery, Shared.Common.PageResult<UserDto>> getUsersHandler)
    {
        _createUserHandler = createUserHandler;
        _updateUserHandler = updateUserHandler;
        _deleteUserHandler = deleteUserHandler;
        _getUserByIdHandler = getUserByIdHandler;
        _getUsersHandler = getUsersHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] Role? role,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetUsersQuery(role, classId, search, page, pageSize);
        var result = await _getUsersHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var result = await _getUserByIdHandler.HandleAsync(new GetUserByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var command = new CreateUserCommand(request.Email, request.FullName, request.Password, request.Role, request.ClassId, request.DepartmentId);
        var result = await _createUserHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetUserById), new { id = result.Value!.Id }, new ApiResponse<UserDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var command = new UpdateUserCommand(id, request.FullName, request.Password, request.ClassId, request.DepartmentId);
        var result = await _updateUserHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _deleteUserHandler.HandleAsync(new DeleteUserCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateUserRequest(string Email, string FullName, string Password, Role Role, Guid? ClassId, Guid? DepartmentId);
public sealed record UpdateUserRequest(string FullName, string? Password, Guid? ClassId, Guid? DepartmentId);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.")
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid role is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("A student must be assigned to a class.")
            .When(x => x.Role == Role.Student);

        RuleFor(x => x.ClassId)
            .Empty().WithMessage("Only students may be assigned to a class.")
            .When(x => x.Role != Role.Student);

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("A teacher must be assigned to a department.")
            .When(x => x.Role == Role.Teacher);

        RuleFor(x => x.DepartmentId)
            .Empty().WithMessage("Only teachers may be assigned to a department.")
            .When(x => x.Role != Role.Teacher);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Password)
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}
