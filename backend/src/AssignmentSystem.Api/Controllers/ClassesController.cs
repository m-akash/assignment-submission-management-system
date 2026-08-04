using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Classes;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/classes")]
[Authorize]
public sealed class ClassesController : ControllerBase
{
    private readonly ICommandHandler<CreateClassCommand, ClassDto> _createClassHandler;
    private readonly ICommandHandler<UpdateClassCommand, ClassDto> _updateClassHandler;
    private readonly ICommandHandler<DeleteClassCommand> _deleteClassHandler;
    private readonly IQueryHandler<GetClassByIdQuery, ClassDto> _getClassByIdHandler;
    private readonly IQueryHandler<GetClassesQuery, Shared.Common.PageResult<ClassDto>> _getClassesHandler;

    public ClassesController(
        ICommandHandler<CreateClassCommand, ClassDto> createClassHandler,
        ICommandHandler<UpdateClassCommand, ClassDto> updateClassHandler,
        ICommandHandler<DeleteClassCommand> deleteClassHandler,
        IQueryHandler<GetClassByIdQuery, ClassDto> getClassByIdHandler,
        IQueryHandler<GetClassesQuery, Shared.Common.PageResult<ClassDto>> getClassesHandler)
    {
        _createClassHandler = createClassHandler;
        _updateClassHandler = updateClassHandler;
        _deleteClassHandler = deleteClassHandler;
        _getClassByIdHandler = getClassByIdHandler;
        _getClassesHandler = getClassesHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetClasses(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetClassesQuery(search, page, pageSize);
        var result = await _getClassesHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetClassById(Guid id, CancellationToken ct)
    {
        var result = await _getClassByIdHandler.HandleAsync(new GetClassByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        var command = new CreateClassCommand(request.Name, request.Level, request.Section);
        var result = await _createClassHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetClassById), new { id = result.Value!.Id }, new ApiResponse<ClassDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] UpdateClassRequest request, CancellationToken ct)
    {
        var command = new UpdateClassCommand(id, request.Name, request.Level, request.Section);
        var result = await _updateClassHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken ct)
    {
        var result = await _deleteClassHandler.HandleAsync(new DeleteClassCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateClassRequest(string Name, int Level, string? Section);
public sealed record UpdateClassRequest(string Name, int Level, string? Section);

public sealed class CreateClassRequestValidator : AbstractValidator<CreateClassRequest>
{
    public CreateClassRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(150).WithMessage("Class name cannot exceed 150 characters.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 12).WithMessage("Class level must be between 1 and 12.");

        RuleFor(x => x.Section)
            .MaximumLength(50).WithMessage("Section cannot exceed 50 characters.");
    }
}

public sealed class UpdateClassRequestValidator : AbstractValidator<UpdateClassRequest>
{
    public UpdateClassRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(150).WithMessage("Class name cannot exceed 150 characters.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 12).WithMessage("Class level must be between 1 and 12.");

        RuleFor(x => x.Section)
            .MaximumLength(50).WithMessage("Section cannot exceed 50 characters.");
    }
}
