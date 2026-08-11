using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Pre-aggregated chart data for the overview screens — one endpoint per role, because the
/// three dashboards answer different questions from different scopes.
///
/// These exist because the tiles above the charts can be counted with
/// <c>?pageSize=1</c> and a pagination total, but a trend or a distribution cannot: the
/// alternative is shipping every submission to the browser and grouping it there, which stops
/// working at the first school with real data in it.
///
/// Each route carries its own role gate, and so does the query behind it. Neither is
/// redundant: the attribute here keeps a wrong-role caller out of the pipeline, and the
/// attribute on the message is what the startup check validates.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public DashboardController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>
    /// School-wide: activity per day, the draft/published split, and each class's submission
    /// rate. <paramref name="days"/> is clamped to the window bounds rather than rejected.
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminDashboard(
        [FromQuery] int days = DashboardWindow.DefaultDays, CancellationToken ct = default)
    {
        var result = await _dispatcher.QueryAsync(new GetAdminDashboardQuery(days), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// The signed-in teacher's own work: per-assignment progress, marking throughput, and the
    /// spread of marks they have given. No teacher id in the route — it comes from the token.
    /// </summary>
    [HttpGet("teacher")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetTeacherDashboard(
        [FromQuery] int days = DashboardWindow.DefaultDays, CancellationToken ct = default)
    {
        var result = await _dispatcher.QueryAsync(new GetTeacherDashboardQuery(days), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// The signed-in student's own marks over time, their average per course, and whether
    /// their work arrives on time. Self-scoped for the same reason.
    /// </summary>
    [HttpGet("student")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetStudentDashboard(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetStudentDashboardQuery(), ct);
        return result.ToActionResult(this);
    }
}
