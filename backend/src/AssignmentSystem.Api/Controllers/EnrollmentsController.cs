using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Enrollments;
using AssignmentSystem.Application.Features.StudentCourses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Student↔class memberships. Writing enrollments is Admin-only: enrollment decides what
/// a student can see and submit to (rule B1), so it is exactly the kind of thing a student
/// must not be able to change for themselves. The list endpoint is open to Teachers (scoped
/// by <see cref="GetEnrollmentsHandler"/> to taught classes) and Admins. The student-facing
/// <c>me/courses</c> read is Student-only, so role restrictions live on each action rather
/// than the class — a method-level role attribute composes with (rather than overrides) the
/// class attribute, so a shared class guard would lock students out of their own endpoint.
/// </summary>
[ApiController]
[Route("api/v1/enrollments")]
[Authorize]
public sealed class EnrollmentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public EnrollmentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? classId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetEnrollmentsQuery(studentId, classId, search, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    /// <summary>
    /// The signed-in student's own courses and the teacher(s) for each. Self-scoped server-
    /// side: no id travels in the query, mirroring the rule-B1 read the rest of the student
    /// experience depends on.
    /// </summary>
    [HttpGet("me/courses")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyCourses(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetStudentCoursesQuery(search, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateEnrollment([FromBody] CreateEnrollmentRequest request, CancellationToken ct)
    {
        var command = new CreateEnrollmentCommand(request.StudentId, request.ClassId);
        var result = await _dispatcher.SendAsync(command, ct);
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEnrollment(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteEnrollmentCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateEnrollmentRequest(Guid StudentId, Guid ClassId);

