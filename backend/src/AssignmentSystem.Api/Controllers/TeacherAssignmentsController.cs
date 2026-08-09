using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.TeacherAssignments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/teacher-assignments")]
[Authorize]
public sealed class TeacherAssignmentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TeacherAssignmentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetTeacherAssignments(
        [FromQuery(Name = "teacherId")] Guid[]? teacherIds,
        [FromQuery(Name = "courseId")] Guid[]? courseIds,
        [FromQuery(Name = "classId")] Guid[]? classIds,
        [FromQuery(Name = "classCourseId")] Guid[]? classCourseIds,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetTeacherAssignmentsQuery(teacherIds, courseIds, classIds, classCourseIds, search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
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
        var result = await _dispatcher.SendAsync(command, ct);
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
        var result = await _dispatcher.SendAsync(new DeleteTeacherAssignmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

/// <summary>
/// The client sends the offering, not a (class, course) pair — the pair it would otherwise
/// send could name a combination the class does not study.
/// </summary>
public sealed record CreateTeacherAssignmentRequest(Guid TeacherId, Guid ClassCourseId);

