using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.TeacherAssignments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/teacher-assignments")]
[Authorize]
public sealed class TeacherAssignmentsController : ControllerBase
{
    private readonly ICommandHandler<CreateTeacherAssignmentCommand, TeacherAssignmentDto> _createHandler;
    private readonly ICommandHandler<DeleteTeacherAssignmentCommand> _deleteHandler;
    private readonly IQueryHandler<GetTeacherAssignmentsQuery, Shared.Common.PageResult<TeacherAssignmentDto>> _getQueryHandler;

    public TeacherAssignmentsController(
        ICommandHandler<CreateTeacherAssignmentCommand, TeacherAssignmentDto> createHandler,
        ICommandHandler<DeleteTeacherAssignmentCommand> deleteHandler,
        IQueryHandler<GetTeacherAssignmentsQuery, Shared.Common.PageResult<TeacherAssignmentDto>> getQueryHandler)
    {
        _createHandler = createHandler;
        _deleteHandler = deleteHandler;
        _getQueryHandler = getQueryHandler;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetTeacherAssignments(
        [FromQuery] Guid? teacherId,
        [FromQuery] Guid? courseId,
        [FromQuery] Guid? classId,
        [FromQuery] Guid? classCourseId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetTeacherAssignmentsQuery(teacherId, courseId, classId, classCourseId, search, page, pageSize);
        var result = await _getQueryHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateTeacherAssignment([FromBody] CreateTeacherAssignmentRequest request, CancellationToken ct)
    {
        var command = new CreateTeacherAssignmentCommand(request.TeacherId, request.ClassCourseId);
        var result = await _createHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return Ok(new ApiResponse<TeacherAssignmentDto> { Success = true, Data = result.Value });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTeacherAssignment(Guid id, CancellationToken ct)
    {
        var result = await _deleteHandler.HandleAsync(new DeleteTeacherAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

/// <summary>
/// The client sends the offering, not a (class, course) pair — the pair it would otherwise
/// send could name a combination the class does not study.
/// </summary>
public sealed record CreateTeacherAssignmentRequest(Guid TeacherId, Guid ClassCourseId);

public sealed class CreateTeacherAssignmentRequestValidator : AbstractValidator<CreateTeacherAssignmentRequest>
{
    public CreateTeacherAssignmentRequestValidator()
    {
        RuleFor(x => x.TeacherId)
            .NotEmpty().WithMessage("Teacher id is required.");

        RuleFor(x => x.ClassCourseId)
            .NotEmpty().WithMessage("Choose the class and course to assign the teacher to.");
    }
}
