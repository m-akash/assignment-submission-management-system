using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Subjects;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/subjects")]
[Authorize]
public sealed class SubjectsController : ControllerBase
{
    private readonly ICommandHandler<CreateSubjectCommand, SubjectDto> _createSubjectHandler;
    private readonly ICommandHandler<UpdateSubjectCommand, SubjectDto> _updateSubjectHandler;
    private readonly ICommandHandler<DeleteSubjectCommand> _deleteSubjectHandler;
    private readonly IQueryHandler<GetSubjectByIdQuery, SubjectDto> _getSubjectByIdHandler;
    private readonly IQueryHandler<GetSubjectsQuery, Shared.Common.PageResult<SubjectDto>> _getSubjectsHandler;

    public SubjectsController(
        ICommandHandler<CreateSubjectCommand, SubjectDto> createSubjectHandler,
        ICommandHandler<UpdateSubjectCommand, SubjectDto> updateSubjectHandler,
        ICommandHandler<DeleteSubjectCommand> deleteSubjectHandler,
        IQueryHandler<GetSubjectByIdQuery, SubjectDto> getSubjectByIdHandler,
        IQueryHandler<GetSubjectsQuery, Shared.Common.PageResult<SubjectDto>> getSubjectsHandler)
    {
        _createSubjectHandler = createSubjectHandler;
        _updateSubjectHandler = updateSubjectHandler;
        _deleteSubjectHandler = deleteSubjectHandler;
        _getSubjectByIdHandler = getSubjectByIdHandler;
        _getSubjectsHandler = getSubjectsHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetSubjectsQuery(search, page, pageSize);
        var result = await _getSubjectsHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSubjectById(Guid id, CancellationToken ct)
    {
        var result = await _getSubjectByIdHandler.HandleAsync(new GetSubjectByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request, CancellationToken ct)
    {
        var command = new CreateSubjectCommand(request.Name, request.Code);
        var result = await _createSubjectHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetSubjectById), new { id = result.Value!.Id }, new ApiResponse<SubjectDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequest request, CancellationToken ct)
    {
        var command = new UpdateSubjectCommand(id, request.Name, request.Code);
        var result = await _updateSubjectHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSubject(Guid id, CancellationToken ct)
    {
        var result = await _deleteSubjectHandler.HandleAsync(new DeleteSubjectCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateSubjectRequest(string Name, string Code);
public sealed record UpdateSubjectRequest(string Name, string Code);

public sealed class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(150).WithMessage("Subject name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Subject code is required.")
            .MaximumLength(30).WithMessage("Subject code cannot exceed 30 characters.");
    }
}

public sealed class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequest>
{
    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(150).WithMessage("Subject name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Subject code is required.")
            .MaximumLength(30).WithMessage("Subject code cannot exceed 30 characters.");
    }
}
