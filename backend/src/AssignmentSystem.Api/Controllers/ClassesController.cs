using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

[ApiController]
[Route("api/v1/classes")]
[Authorize]
public sealed class ClassesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ClassesController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> GetClasses(
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetClassesQuery(search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetClassById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetClassByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        var command = new CreateClassCommand(request.Name, request.Level, request.Section);
        var result = await _dispatcher.SendAsync(command, ct);
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
        var result = await _dispatcher.SendAsync(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteClassCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateClassRequest(string Name, int Level, string? Section);
public sealed record UpdateClassRequest(string Name, int Level, string? Section);

