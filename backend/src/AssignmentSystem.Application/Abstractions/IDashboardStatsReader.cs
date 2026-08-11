namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The aggregates the three dashboards chart. A specific port for the same reason
/// <see cref="IClassCourseUsageReader"/> is one: every number here is a GROUP BY over rows
/// nobody wants loaded, which is not expressible as a Specification.
///
/// The port takes ids rather than deriving them. Which assignments are a teacher's, and
/// which classes a student sits in, are authorization answers — they belong to the handler
/// that already knows how to ask them (rule B1 through <see cref="IClassRosterRepository"/>,
/// rule B3 through the assignment's author column), not to a reader that would end up
/// re-deriving scope a second way.
/// </summary>
public interface IDashboardStatsReader
{
    /// <summary>School-wide totals. Admin-scoped by the caller; nothing here is filtered.</summary>
    Task<AdminDashboardStats> GetAdminStatsAsync(int trendDays, CancellationToken ct = default);

    /// <summary>
    /// Everything scoped to work <paramref name="teacherId"/> authored — the ownership column
    /// on the assignment, which is the same thing rule B3 checks.
    /// </summary>
    Task<TeacherDashboardStats> GetTeacherStatsAsync(
        Guid teacherId, int trendDays, int assignmentLimit, CancellationToken ct = default);

    /// <summary>
    /// The student's own marks, plus what their classes were set.
    /// <paramref name="classIds"/> is the caller's rule-B1 read: passing an empty set yields
    /// empty series rather than the whole school's.
    /// </summary>
    Task<StudentDashboardStats> GetStudentStatsAsync(
        Guid studentId,
        IReadOnlyCollection<Guid> classIds,
        int markLimit,
        CancellationToken ct = default);
}

// ── Series shapes ─────────────────────────────────────────────────────────────
// Declared here beside the port, as ClassCourseUsage is: they are what the reader answers
// with, and the dashboard queries return them unchanged rather than copying every field
// into a parallel set of DTOs that could only ever disagree.

/// <summary>
/// One day of activity. <see cref="Date"/> is a calendar day in UTC — the same basis every
/// stored instant uses, so a bucket cannot drift with the reader's time zone.
/// </summary>
public sealed record DailyActivityPoint(DateOnly Date, int Submitted, int Graded);

/// <summary>Assignments by publication state — the whole of it, so the two add up to the total.</summary>
public sealed record AssignmentStatusBreakdown(int Draft, int Published);

/// <summary>
/// How much of what a class was set has actually arrived.
/// <see cref="Expected"/> is students × published assignments for the class, so
/// <see cref="Received"/> over it is the class's submission rate. Expected is zero for a
/// class with no students or nothing published yet — callers must not divide blindly.
/// </summary>
public sealed record ClassActivityStat(
    Guid ClassId,
    int ClassLevel,
    string? ClassSection,
    int Students,
    int PublishedAssignments,
    int Expected,
    int Received);

/// <summary>
/// One published assignment split three ways over the class it was set for. The three counts
/// sum to the class roster: a student is either marked, waiting to be marked, or has not
/// handed in. A submission still in Pending has files uploaded but was never submitted, so it
/// counts as not handed in — which is what the student's teacher is actually waiting on.
/// </summary>
public sealed record AssignmentProgressStat(
    Guid AssignmentId,
    string Title,
    DateTime DeadlineUtc,
    int Graded,
    int AwaitingMarking,
    int NotSubmitted);

/// <summary>
/// A band of the percentage scale and how many marks fell in it. Percentages rather than raw
/// marks: two assignments with different maximums are otherwise not comparable.
/// </summary>
public sealed record GradeBandStat(string Band, int Count);

/// <summary>One graded piece of work, as a percentage, in the order it was marked.</summary>
public sealed record MarkPointStat(
    Guid SubmissionId,
    string AssignmentTitle,
    string CourseCode,
    DateTime ReviewedAtUtc,
    decimal Percent);

/// <summary>A student's average across one course, as a percentage.</summary>
public sealed record CourseAverageStat(
    Guid CourseId,
    string CourseName,
    string CourseCode,
    decimal AveragePercent,
    int GradedCount);

/// <summary>
/// Handed-in work against its deadline, plus what never arrived.
/// On-time versus late is decided by comparing timestamps, not by reading
/// <c>SubmissionStatus.Late</c> — grading a late submission overwrites that status, so the
/// status alone would quietly reclassify late work as on-time the moment it was marked.
/// </summary>
public sealed record SubmissionTimelinessStat(int OnTime, int Late, int NotSubmitted);

public sealed record AdminDashboardStats(
    IReadOnlyList<DailyActivityPoint> ActivityTrend,
    AssignmentStatusBreakdown AssignmentStatus,
    IReadOnlyList<ClassActivityStat> ClassActivity);

public sealed record TeacherDashboardStats(
    IReadOnlyList<AssignmentProgressStat> AssignmentProgress,
    IReadOnlyList<DailyActivityPoint> MarkingTrend,
    IReadOnlyList<GradeBandStat> GradeDistribution);

public sealed record StudentDashboardStats(
    IReadOnlyList<MarkPointStat> MarksOverTime,
    IReadOnlyList<CourseAverageStat> CourseAverages,
    SubmissionTimelinessStat Timeliness);
