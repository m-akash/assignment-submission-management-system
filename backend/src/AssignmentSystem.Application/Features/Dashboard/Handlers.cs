using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Dashboard;

/// <summary>
/// School-wide chart data. Nothing to scope — the role gate on the query is the whole of the
/// authorization, which is why this handler is only a clamp and a call.
/// </summary>
public sealed class GetAdminDashboardHandler : IQueryHandler<GetAdminDashboardQuery, AdminDashboardStats>
{
    private readonly IDashboardStatsReader _stats;

    public GetAdminDashboardHandler(IDashboardStatsReader stats) => _stats = stats;

    public async Task<Result<AdminDashboardStats>> HandleAsync(
        GetAdminDashboardQuery query, CancellationToken ct = default)
        => await _stats.GetAdminStatsAsync(DashboardWindow.ClampDays(query.Days), ct);
}

/// <summary>
/// Chart data for the signed-in teacher. The teacher id comes from the token, never from the
/// request: with a caller-supplied id this endpoint would hand one teacher another's marking
/// backlog and grade spread.
/// </summary>
public sealed class GetTeacherDashboardHandler : IQueryHandler<GetTeacherDashboardQuery, TeacherDashboardStats>
{
    private readonly IDashboardStatsReader _stats;
    private readonly ICurrentUser _currentUser;

    public GetTeacherDashboardHandler(IDashboardStatsReader stats, ICurrentUser currentUser)
    {
        _stats = stats;
        _currentUser = currentUser;
    }

    public async Task<Result<TeacherDashboardStats>> HandleAsync(
        GetTeacherDashboardQuery query, CancellationToken ct = default)
    {
        // The role is enforced by the pipeline; the id is still checked, because a token that
        // authenticates but carries no subject claim would otherwise query for Guid.Empty and
        // answer with an empty dashboard instead of saying the session is unusable.
        if (_currentUser.UserId is null)
        {
            return Result<TeacherDashboardStats>.Failure(Error.Unauthorized(
                "Dashboard.NoIdentity", "Your session does not identify a teacher account."));
        }

        return await _stats.GetTeacherStatsAsync(
            _currentUser.UserId.Value,
            DashboardWindow.ClampDays(query.Days),
            DashboardWindow.AssignmentLimit,
            ct);
    }
}

/// <summary>
/// Chart data for the signed-in student. Their classes are read through
/// <see cref="IClassRosterRepository"/> rather than taken from a token claim — the same
/// rule-B1 read the assignment list uses, so an admin moving a student between classes is
/// reflected on the next request.
/// </summary>
public sealed class GetStudentDashboardHandler : IQueryHandler<GetStudentDashboardQuery, StudentDashboardStats>
{
    private readonly IDashboardStatsReader _stats;
    private readonly IClassRosterRepository _roster;
    private readonly ICurrentUser _currentUser;

    public GetStudentDashboardHandler(
        IDashboardStatsReader stats,
        IClassRosterRepository roster,
        ICurrentUser currentUser)
    {
        _stats = stats;
        _roster = roster;
        _currentUser = currentUser;
    }

    public async Task<Result<StudentDashboardStats>> HandleAsync(
        GetStudentDashboardQuery query, CancellationToken ct = default)
    {
        if (_currentUser.UserId is null)
        {
            return Result<StudentDashboardStats>.Failure(Error.Unauthorized(
                "Dashboard.NoIdentity", "Your session does not identify a student account."));
        }

        var classIds = await _roster.GetEnrolledClassIdsAsync(_currentUser.UserId.Value, ct);

        return await _stats.GetStudentStatsAsync(
            _currentUser.UserId.Value, classIds, DashboardWindow.MarkLimit, ct);
    }
}
