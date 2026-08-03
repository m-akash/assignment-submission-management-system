using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Departments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize]
public sealed class DepartmentsController : ControllerBase
{
    private readonly ICommandHandler<CreateDepartmentCommand, DepartmentDto> _createHandler;
    private readonly ICommandHandler<UpdateDepartmentCommand, DepartmentDto> _updateHandler;
    private readonly ICommandHandler<DeleteDepartmentCommand> _deleteHandler;
    private readonly IQueryHandler<GetDepartmentByIdQuery, DepartmentDto> _getByIdHandler;
    private readonly IQueryHandler<GetDepartmentsQuery, Shared.Common.PageResult<DepartmentDto>> _getListHandler;

    public DepartmentsController(
        ICommandHandler<CreateDepartmentCommand, DepartmentDto> createHandler,
        ICommandHandler<UpdateDepartmentCommand, DepartmentDto> updateHandler,
        ICommandHandler<DeleteDepartmentCommand> deleteHandler,
        IQueryHandler<GetDepartmentByIdQuery, DepartmentDto> getByIdHandler,
        IQueryHandler<GetDepartmentsQuery, Shared.Common.PageResult<DepartmentDto>> getListHandler)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _getByIdHandler = getByIdHandler;
        _getListHandler = getListHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _getListHandler.HandleAsync(new GetDepartmentsQuery(search, page, pageSize), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDepartmentById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(new GetDepartmentByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _createHandler.HandleAsync(new CreateDepartmentCommand(request.Name, request.Code), ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetDepartmentById), new { id = result.Value!.Id },
            new ApiResponse<DepartmentDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
    {
        var result = await _updateHandler.HandleAsync(new UpdateDepartmentCommand(id, request.Name, request.Code), ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct)
    {
        var result = await _deleteHandler.HandleAsync(new DeleteDepartmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateDepartmentRequest(string Name, string Code);
public sealed record UpdateDepartmentRequest(string Name, string Code);

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(150).WithMessage("Department name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Department code is required.")
            .MaximumLength(10).WithMessage("Department code cannot exceed 10 characters.");
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(150).WithMessage("Department name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Department code is required.")
            .MaximumLength(10).WithMessage("Department code cannot exceed 10 characters.");
    }
}
