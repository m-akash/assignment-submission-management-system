using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Enrollments;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Student↔class memberships. Admin-only: enrollment decides what a student can see and
/// submit to (rule B1), so it is exactly the kind of thing a student must not be able to
/// change for themselves.
/// </summary>
[ApiController]
[Route("api/v1/enrollments")]
[Authorize(Roles = "Admin")]
public sealed class EnrollmentsController : ControllerBase
{
    private readonly ICommandHandler<CreateEnrollmentCommand, EnrollmentDto> _createHandler;
    private readonly ICommandHandler<DeleteEnrollmentCommand> _deleteHandler;
    private readonly IQueryHandler<GetEnrollmentsQuery, Shared.Common.PageResult<EnrollmentDto>> _getListHandler;

    public EnrollmentsController(
        ICommandHandler<CreateEnrollmentCommand, EnrollmentDto> createHandler,
        ICommandHandler<DeleteEnrollmentCommand> deleteHandler,
        IQueryHandler<GetEnrollmentsQuery, Shared.Common.PageResult<EnrollmentDto>> getListHandler)
    {
        _createHandler = createHandler;
        _deleteHandler = deleteHandler;
        _getListHandler = getListHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetEnrollmentsQuery(studentId, classId, search, page, pageSize);
        var result = await _getListHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEnrollment([FromBody] CreateEnrollmentRequest request, CancellationToken ct)
    {
        var command = new CreateEnrollmentCommand(request.StudentId, request.ClassId);
        var result = await _createHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return Ok(new ApiResponse<EnrollmentDto> { Success = true, Data = result.Value });
    }

    /// <summary>
    /// Refused for a student's only class — see <c>DeleteEnrollmentHandler</c>. To move a
    /// student, POST the new enrollment first, then DELETE the old one.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEnrollment(Guid id, CancellationToken ct)
    {
        var result = await _deleteHandler.HandleAsync(new DeleteEnrollmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateEnrollmentRequest(Guid StudentId, Guid ClassId);

public sealed class CreateEnrollmentRequestValidator : AbstractValidator<CreateEnrollmentRequest>
{
    public CreateEnrollmentRequestValidator()
    {
        RuleFor(x => x.StudentId)
            .NotEmpty().WithMessage("Student id is required.");

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class id is required.");
    }
}
