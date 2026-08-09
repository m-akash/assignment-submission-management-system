using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.AcademicYears;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// The school's sessions. Reads are open to any signed-in user — an enrollment names its
/// year, so students and teachers see the label on their own class lists — while writing
/// stays with the Admin, like the rest of the reference data.
/// </summary>
[ApiController]
[Route("api/v1/academic-years")]
[Authorize]
public sealed class AcademicYearsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AcademicYearsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> GetAcademicYears(
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetAcademicYearsQuery(search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAcademicYearById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetAcademicYearByIdQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAcademicYear([FromBody] CreateAcademicYearRequest request, CancellationToken ct)
    {
        var command = new CreateAcademicYearCommand(request.Name, request.StartDate, request.EndDate, request.IsCurrent);
        var result = await _dispatcher.SendAsync(command, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return CreatedAtAction(
            nameof(GetAcademicYearById),
            new { id = result.Value!.Id },
            new ApiResponse<AcademicYearDto> { Success = true, Data = result.Value });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAcademicYear(Guid id, [FromBody] UpdateAcademicYearRequest request, CancellationToken ct)
    {
        var command = new UpdateAcademicYearCommand(id, request.Name, request.StartDate, request.EndDate, request.IsCurrent);
        var result = await _dispatcher.SendAsync(command, ct);
        return result.ToActionResult(this);
    }

    /// <summary>Refused for a year that has enrollments — see <c>DeleteAcademicYearHandler</c>.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAcademicYear(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new DeleteAcademicYearCommand(id), ct);
        return result.ToActionResult(this);
    }
}

public sealed record CreateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);
public sealed record UpdateAcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);
