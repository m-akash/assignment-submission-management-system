using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Courses;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/courses")]
[Authorize]
public sealed class CoursesController : ControllerBase
{
    private readonly ICommandHandler<CreateCourseCommand, CourseDto> _createCourseHandler;
    private readonly ICommandHandler<UpdateCourseCommand, CourseDto> _updateCourseHandler;
    private readonly ICommandHandler<DeleteCourseCommand> _deleteCourseHandler;
    private readonly IQueryHandler<GetCourseByIdQuery, CourseDto> _getCourseByIdHandler;
    private readonly IQueryHandler<GetCoursesQuery, Shared.Common.PageResult<CourseDto>> _getCoursesHandler;

    public CoursesController(
        ICommandHandler<CreateCourseCommand, CourseDto> createCourseHandler,
        ICommandHandler<UpdateCourseCommand, CourseDto> updateCourseHandler,
        ICommandHandler<DeleteCourseCommand> deleteCourseHandler,
        IQueryHandler<GetCourseByIdQuery, CourseDto> getCourseByIdHandler,
        IQueryHandler<GetCoursesQuery, Shared.Common.PageResult<CourseDto>> getCoursesHandler)
    {
        _createCourseHandler = createCourseHandler;
        _updateCourseHandler = updateCourseHandler;
        _deleteCourseHandler = deleteCourseHandler;
        _getCourseByIdHandler = getCourseByIdHandler;
        _getCoursesHandler = getCoursesHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetCoursesQuery(search, page, pageSize);
        var result = await _getCoursesHandler.HandleAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCourseById(Guid id, CancellationToken ct)
    {
        var result = await _getCourseByIdHandler.HandleAsync(new GetCourseByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        var command = new CreateCourseCommand(request.Name, request.Code);
        var result = await _createCourseHandler.HandleAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Value!.Id }, new ApiResponse<CourseDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCourse(Guid id, [FromBody] UpdateCourseRequest request, CancellationToken ct)
    {
        var command = new UpdateCourseCommand(id, request.Name, request.Code);
        var result = await _updateCourseHandler.HandleAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCourse(Guid id, CancellationToken ct)
    {
        var result = await _deleteCourseHandler.HandleAsync(new DeleteCourseCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateCourseRequest(string Name, string Code);
public sealed record UpdateCourseRequest(string Name, string Code);

public sealed class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Course name is required.")
            .MaximumLength(150).WithMessage("Course name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Course code is required.")
            .MaximumLength(30).WithMessage("Course code cannot exceed 30 characters.");
    }
}

public sealed class UpdateCourseRequestValidator : AbstractValidator<UpdateCourseRequest>
{
    public UpdateCourseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Course name is required.")
            .MaximumLength(150).WithMessage("Course name cannot exceed 150 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Course code is required.")
            .MaximumLength(30).WithMessage("Course code cannot exceed 30 characters.");
    }
}
