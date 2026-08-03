using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Assignments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/assignments")]
[Authorize]
public sealed class AssignmentsController : ControllerBase
{
    private readonly ICommandHandler<CreateAssignmentCommand, AssignmentDto> _createHandler;
    private readonly ICommandHandler<UpdateAssignmentCommand, AssignmentDto> _updateHandler;
    private readonly ICommandHandler<DeleteAssignmentCommand> _deleteHandler;
    private readonly ICommandHandler<PublishAssignmentCommand, AssignmentDto> _publishHandler;
    private readonly IQueryHandler<GetAssignmentByIdQuery, AssignmentDto> _getByIdHandler;
    private readonly IQueryHandler<GetAssignmentsQuery, Shared.Common.PageResult<AssignmentDto>> _getListHandler;

    public AssignmentsController(
        ICommandHandler<CreateAssignmentCommand, AssignmentDto> createHandler,
        ICommandHandler<UpdateAssignmentCommand, AssignmentDto> updateHandler,
        ICommandHandler<DeleteAssignmentCommand> deleteHandler,
        ICommandHandler<PublishAssignmentCommand, AssignmentDto> publishHandler,
        IQueryHandler<GetAssignmentByIdQuery, AssignmentDto> getByIdHandler,
        IQueryHandler<GetAssignmentsQuery, Shared.Common.PageResult<AssignmentDto>> getListHandler)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _publishHandler = publishHandler;
        _getByIdHandler = getByIdHandler;
        _getListHandler = getListHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? classId,
        [FromQuery] Guid? courseId,
        [FromQuery] Guid? teacherId,
        [FromQuery] Domain.Enums.AssignmentStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetAssignmentsQuery(classId, courseId, teacherId, status, search, page, pageSize);
        var result = await _getListHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAssignmentById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.HandleAsync(new GetAssignmentByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateAssignmentRequest request, CancellationToken ct)
    {
        var command = new CreateAssignmentCommand(
            request.TeacherAssignmentId,
            request.Title,
            request.Description,
            request.DeadlineUtc,
            request.MaxMarks,
            request.AllowResubmission);

        var result = await _createHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetAssignmentById), new { id = result.Value!.Id }, new ApiResponse<AssignmentDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UpdateAssignment(Guid id, [FromBody] UpdateAssignmentRequest request, CancellationToken ct)
    {
        var command = new UpdateAssignmentCommand(
            id,
            request.Title,
            request.Description,
            request.DeadlineUtc,
            request.MaxMarks,
            request.AllowResubmission);

        var result = await _updateHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> PublishAssignment(Guid id, CancellationToken ct)
    {
        var result = await _publishHandler.HandleAsync(new PublishAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        var result = await _deleteHandler.HandleAsync(new DeleteAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateAssignmentRequest(
    Guid TeacherAssignmentId,
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission);

public sealed record UpdateAssignmentRequest(
    string Title,
    string Description,
    DateTime DeadlineUtc,
    decimal MaxMarks,
    bool AllowResubmission);

public sealed class CreateAssignmentRequestValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentRequestValidator()
    {
        RuleFor(x => x.TeacherAssignmentId)
            .NotEmpty().WithMessage("Teacher assignment id is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.DeadlineUtc)
            .NotEmpty().WithMessage("Deadline is required.");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0).WithMessage("Maximum marks must be greater than zero.");
    }
}

public sealed class UpdateAssignmentRequestValidator : AbstractValidator<UpdateAssignmentRequest>
{
    public UpdateAssignmentRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.DeadlineUtc)
            .NotEmpty().WithMessage("Deadline is required.");

        RuleFor(x => x.MaxMarks)
            .GreaterThan(0).WithMessage("Maximum marks must be greater than zero.");
    }
}
