using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.ClassCourses;
using FluentValidation;
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
    private readonly ICommandHandler<CreateClassCourseCommand, ClassCourseDto> _createHandler;
    private readonly ICommandHandler<DeleteClassCourseCommand> _deleteHandler;
    private readonly IQueryHandler<GetClassCourseByIdQuery, ClassCourseDto> _getByIdHandler;
    private readonly IQueryHandler<GetClassCoursesQuery, Shared.Common.PageResult<ClassCourseDto>> _getListHandler;

    public ClassCoursesController(
        ICommandHandler<CreateClassCourseCommand, ClassCourseDto> createHandler,
        ICommandHandler<DeleteClassCourseCommand> deleteHandler,
        IQueryHandler<GetClassCourseByIdQuery, ClassCourseDto> getByIdHandler,
        IQueryHandler<GetClassCoursesQuery, Shared.Common.PageResult<ClassCourseDto>> getListHandler)
    {
        _createHandler = createHandler;
        _deleteHandler = deleteHandler;
        _getByIdHandler = getByIdHandler;
        _getListHandler = getListHandler;
    }

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
        var result = await _getListHandler.HandleAsync(query, ct);
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
        var result = await _getByIdHandler.HandleAsync(new GetClassCourseByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClassCourse([FromBody] CreateClassCourseRequest request, CancellationToken ct)
    {
        var command = new CreateClassCourseCommand(request.ClassId, request.CourseId);
        var result = await _createHandler.HandleAsync(command, ct);
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
        var result = await _deleteHandler.HandleAsync(new DeleteClassCourseCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateClassCourseRequest(Guid ClassId, Guid CourseId);

public sealed class CreateClassCourseRequestValidator : AbstractValidator<CreateClassCourseRequest>
{
    public CreateClassCourseRequestValidator()
    {
        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Class id is required.");

        RuleFor(x => x.CourseId)
            .NotEmpty().WithMessage("Course id is required.");
    }
}
