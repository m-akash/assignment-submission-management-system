using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.ClassCourses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Course offerings — which courses a class studies.
///
/// Readable by teachers as well as admins: a teacher's assignment form needs the offerings
/// they are mapped to in order to pick a scope, and the list carries no sensitive data. Only
/// an admin may change the catalogue.
/// </summary>
[ApiController]
[Route("api/v1/class-courses")]
[Authorize]
public sealed class ClassCoursesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ClassCoursesController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetClassCourses(
        [FromQuery] Guid? classId,
        [FromQuery] Guid? courseId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetClassCoursesQuery(classId, courseId, search, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetClassCourseById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetClassCourseByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClassCourse([FromBody] CreateClassCourseRequest request, CancellationToken ct)
    {
        var command = new CreateClassCourseCommand(request.ClassId, request.CourseId);
        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(
            nameof(GetClassCourseById),
            new { id = result.Value!.Id },
            new ApiResponse<ClassCourseDto> { Success = true, Data = result.Value });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClassCourse(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteClassCourseCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateClassCourseRequest(Guid ClassId, Guid CourseId);

