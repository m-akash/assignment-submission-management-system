using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Dashboard;

/// <summary>
/// The charts on the admin overview: activity over the last <paramref name="Days"/> days,
/// the draft/published split, and how much of what each class was set has arrived.
///
/// One query per role rather than one parameterised query, because the three answer
/// different questions from different scopes — and a role gate on the message is the only
/// place that fact is enforceable at startup.
/// </summary>
[RequiresRole(Role.Admin)]
public sealed record GetAdminDashboardQuery(int Days = DashboardWindow.DefaultDays)
    : IQuery<AdminDashboardStats>;

/// <summary>
/// The charts on the teacher overview, all scoped to work the caller authored: per-assignment
/// progress, marking throughput, and the spread of marks they have given.
/// </summary>
[RequiresRole(Role.Teacher)]
public sealed record GetTeacherDashboardQuery(int Days = DashboardWindow.DefaultDays)
    : IQuery<TeacherDashboardStats>;

/// <summary>
/// The charts on the student overview: their own marks over time, their average per course,
/// and whether their work arrives on time. Self-scoped — no student id travels in the query.
/// </summary>
[RequiresRole(Role.Student)]
public sealed record GetStudentDashboardQuery : IQuery<StudentDashboardStats>;

/// <summary>
/// Bounds for the trend window, shared by the two queries that take one. Clamped rather than
/// rejected: a dashboard asking for a decade of days wants the longest chart it can have, not
/// a 422 — and clamping here means the ceiling holds whichever caller asks.
/// </summary>
public static class DashboardWindow
{
    /// <summary>Long enough to show a shape, short enough that a young school is not mostly gaps.</summary>
    public const int DefaultDays = 14;

    public const int MinDays = 7;
    public const int MaxDays = 90;

    /// <summary>Assignments on the teacher's progress chart — the most recently due first.</summary>
    public const int AssignmentLimit = 8;

    /// <summary>Graded pieces of work on the student's marks line.</summary>
    public const int MarkLimit = 20;

    public static int ClampDays(int days) => Math.Clamp(days, MinDays, MaxDays);
}
