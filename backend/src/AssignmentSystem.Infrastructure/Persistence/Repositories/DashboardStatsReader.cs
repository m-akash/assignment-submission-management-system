using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// The grouped reads behind the dashboard charts. A handful of aggregate queries per
/// dashboard, never one per row or per chart series.
///
/// Day buckets are computed in memory from the timestamps rather than grouped in SQL. Casting
/// a <c>timestamptz</c> to a date in Postgres resolves against the session time zone, so a
/// SQL-side <c>GROUP BY</c> would put a submission in a different bucket depending on how the
/// connection happened to be configured. Every stored instant means UTC, and so must every
/// bucket. The rows fetched to do that are one small projection over a bounded window.
/// </summary>
internal sealed class DashboardStatsReader : IDashboardStatsReader
{
    /// <summary>
    /// Percentage bands for the marking histogram, each named by the range it covers and
    /// carrying its inclusive upper bound. Ordered, so the first band a percentage fits is
    /// its band; anything above the last bound lands in the last band.
    /// </summary>
    private static readonly (string Band, decimal UpperInclusive)[] GradeBands =
    [
        ("0-20", 20m),
        ("21-40", 40m),
        ("41-60", 60m),
        ("61-80", 80m),
        ("81-100", 100m),
    ];

    private readonly AppDbContext _context;
    private readonly IClock _clock;

    public DashboardStatsReader(AppDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    public async Task<AdminDashboardStats> GetAdminStatsAsync(int trendDays, CancellationToken ct = default)
    {
        var (windowStartUtc, firstDay, lastDay) = Window(trendDays);

        var activity = await _context.Submissions
            .Where(s => (s.SubmittedAtUtc != null && s.SubmittedAtUtc >= windowStartUtc)
                     || (s.ReviewedAtUtc != null && s.ReviewedAtUtc >= windowStartUtc))
            .Select(s => new ActivityRow(s.SubmittedAtUtc, s.ReviewedAtUtc, s.Status))
            .ToListAsync(ct);

        // Both states in one grouped query. The soft-delete filter on assignments already
        // keeps deleted work out, so these two counts add up to the assignments list total.
        var statusCounts = await _context.Assignments
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        var classActivity = await GetClassActivityAsync(ct);

        return new AdminDashboardStats(
            BuildTrend(activity, firstDay, lastDay),
            new AssignmentStatusBreakdown(
                statusCounts.GetValueOrDefault(AssignmentStatus.Draft),
                statusCounts.GetValueOrDefault(AssignmentStatus.Published)),
            classActivity);
    }

    /// <summary>
    /// Per class: roster size, published assignments, and how many submissions arrived
    /// against them. Only classes with something published are returned — a class that has
    /// been set nothing has no submission rate, and a bar at zero would read as "nobody
    /// handed in" rather than "nothing was asked for". Every such class is returned rather
    /// than a top slice, so the chart is never quietly truncated.
    /// </summary>
    private async Task<List<ClassActivityStat>> GetClassActivityAsync(CancellationToken ct)
    {
        var publishedPerClass = await _context.Assignments
            .Where(a => a.Status == AssignmentStatus.Published)
            .GroupBy(a => a.ClassCourse.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);

        if (publishedPerClass.Count == 0)
        {
            return [];
        }

        var classIds = publishedPerClass.Keys.ToList();

        var classes = await _context.Classes
            .Where(c => classIds.Contains(c.Id))
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Section)
            .Select(c => new { c.Id, c.Level, c.Section })
            .ToListAsync(ct);

        var studentCounts = await GetStudentCountsAsync(classIds, ct);

        // Handed in, whatever became of it afterwards — Pending means files were uploaded but
        // never submitted, which is not something the class has actually turned in.
        var receivedPerClass = await _context.Submissions
            .Where(s => s.Status != SubmissionStatus.Pending
                     && classIds.Contains(s.Assignment.ClassCourse.ClassId))
            .GroupBy(s => s.Assignment.ClassCourse.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);

        return [.. classes.Select(c =>
        {
            var students = studentCounts.GetValueOrDefault(c.Id);
            var published = publishedPerClass.GetValueOrDefault(c.Id);
            return new ClassActivityStat(
                c.Id,
                c.Level,
                c.Section,
                students,
                published,
                Expected: students * published,
                Received: receivedPerClass.GetValueOrDefault(c.Id));
        })];
    }

    // ── Teacher ───────────────────────────────────────────────────────────────

    public async Task<TeacherDashboardStats> GetTeacherStatsAsync(
        Guid teacherId, int trendDays, int assignmentLimit, CancellationToken ct = default)
    {
        var (windowStartUtc, firstDay, lastDay) = Window(trendDays);

        // Published only, most recently due first: a draft has no class waiting on it, so it
        // has no progress to show. Ownership is the assignment's author column — the same
        // thing rule B3 checks, rather than a second definition of "the teacher's work".
        var assignments = await _context.Assignments
            .Where(a => a.TeacherId == teacherId && a.Status == AssignmentStatus.Published)
            .OrderByDescending(a => a.DeadlineUtc)
            .Take(assignmentLimit)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.DeadlineUtc,
                ClassId = a.ClassCourse.ClassId,
            })
            .ToListAsync(ct);

        var progress = await BuildAssignmentProgressAsync(assignments
            .Select(a => new AssignmentRef(a.Id, a.Title, a.DeadlineUtc, a.ClassId))
            .ToList(), ct);

        var activity = await _context.Submissions
            .Where(s => s.Assignment.TeacherId == teacherId
                     && ((s.SubmittedAtUtc != null && s.SubmittedAtUtc >= windowStartUtc)
                      || (s.ReviewedAtUtc != null && s.ReviewedAtUtc >= windowStartUtc)))
            .Select(s => new ActivityRow(s.SubmittedAtUtc, s.ReviewedAtUtc, s.Status))
            .ToListAsync(ct);

        // Every mark this teacher has given, not just the window's: the shape of a grade
        // spread is the point, and a fortnight of marking is too few marks to have a shape.
        var marks = await _context.Submissions
            .Where(s => s.Assignment.TeacherId == teacherId
                     && s.Status == SubmissionStatus.Graded
                     && s.Marks != null
                     && s.MarksOutOf != null
                     && s.MarksOutOf > 0)
            .Select(s => new { Marks = s.Marks!.Value, OutOf = s.MarksOutOf!.Value })
            .ToListAsync(ct);

        return new TeacherDashboardStats(
            progress,
            BuildTrend(activity, firstDay, lastDay),
            BuildGradeDistribution(marks.Select(m => Percent(m.Marks, m.OutOf))));
    }

    /// <summary>
    /// Splits each assignment's class three ways. Two grouped queries for the whole chart:
    /// submission counts per (assignment, status), and the roster size of every class
    /// involved.
    /// </summary>
    private async Task<List<AssignmentProgressStat>> BuildAssignmentProgressAsync(
        List<AssignmentRef> assignments, CancellationToken ct)
    {
        if (assignments.Count == 0)
        {
            return [];
        }

        var assignmentIds = assignments.ConvertAll(a => a.Id);
        var classIds = assignments.Select(a => a.ClassId).Distinct().ToList();

        var counts = await _context.Submissions
            .Where(s => assignmentIds.Contains(s.AssignmentId))
            .GroupBy(s => new { s.AssignmentId, s.Status })
            .Select(g => new { g.Key.AssignmentId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var byAssignment = counts
            .GroupBy(row => row.AssignmentId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(row => row.Status, row => row.Count));

        var studentCounts = await GetStudentCountsAsync(classIds, ct);

        return [.. assignments.Select(a =>
        {
            var statuses = byAssignment.GetValueOrDefault(a.Id) ?? [];
            var graded = statuses.GetValueOrDefault(SubmissionStatus.Graded);
            var awaiting = statuses.GetValueOrDefault(SubmissionStatus.Submitted)
                         + statuses.GetValueOrDefault(SubmissionStatus.Late);

            // Clamped at zero: a student who submitted and was later moved out of the class
            // leaves more submissions than there are seats, and a negative bar is nonsense.
            var notSubmitted = Math.Max(0, studentCounts.GetValueOrDefault(a.ClassId) - graded - awaiting);

            return new AssignmentProgressStat(a.Id, a.Title, a.DeadlineUtc, graded, awaiting, notSubmitted);
        })];
    }

    // ── Student ───────────────────────────────────────────────────────────────

    public async Task<StudentDashboardStats> GetStudentStatsAsync(
        Guid studentId,
        IReadOnlyCollection<Guid> classIds,
        int markLimit,
        CancellationToken ct = default)
    {
        // One student's whole graded history, oldest first. Not capped in SQL: the course
        // averages below are an average over all of it, and a capped query would silently
        // average only the most recent marks. It is bounded by how much coursework one
        // student can be set.
        var graded = await _context.Submissions
            .Where(s => s.StudentId == studentId
                     && s.Status == SubmissionStatus.Graded
                     && s.Marks != null
                     && s.MarksOutOf != null
                     && s.MarksOutOf > 0
                     && s.ReviewedAtUtc != null)
            .OrderBy(s => s.ReviewedAtUtc)
            .Select(s => new GradedRow(
                s.Id,
                s.Assignment.Title,
                s.Assignment.ClassCourse.CourseId,
                s.Assignment.ClassCourse.Course.Name,
                s.Assignment.ClassCourse.Course.Code,
                s.ReviewedAtUtc!.Value,
                s.Marks!.Value,
                s.MarksOutOf!.Value))
            .ToListAsync(ct);

        // The tail, still chronological — a line chart reads left to right, and the most
        // recent marks are the ones a student is asking about.
        var marksOverTime = graded
            .Skip(Math.Max(0, graded.Count - markLimit))
            .Select(row => new MarkPointStat(
                row.SubmissionId,
                row.AssignmentTitle,
                row.CourseCode,
                row.ReviewedAtUtc,
                Percent(row.Marks, row.OutOf)))
            .ToList();

        var courseAverages = graded
            .GroupBy(row => (row.CourseId, row.CourseName, row.CourseCode))
            .Select(g => new CourseAverageStat(
                g.Key.CourseId,
                g.Key.CourseName,
                g.Key.CourseCode,
                Math.Round(g.Average(row => Percent(row.Marks, row.OutOf)), 1),
                g.Count()))
            .OrderBy(c => c.CourseName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StudentDashboardStats(
            marksOverTime,
            courseAverages,
            await GetTimelinessAsync(studentId, classIds, ct));
    }

    /// <summary>
    /// On-time, late, and never handed in. The first two come from comparing the submission's
    /// own timestamp with its deadline; the third is what the student's classes were set and
    /// they did not turn in.
    /// </summary>
    private async Task<SubmissionTimelinessStat> GetTimelinessAsync(
        Guid studentId, IReadOnlyCollection<Guid> classIds, CancellationToken ct)
    {
        var handedIn = await _context.Submissions
            .Where(s => s.StudentId == studentId
                     && s.Status != SubmissionStatus.Pending
                     && s.SubmittedAtUtc != null)
            .Select(s => new { SubmittedAtUtc = s.SubmittedAtUtc!.Value, s.Assignment.DeadlineUtc })
            .ToListAsync(ct);

        var onTime = handedIn.Count(row => row.SubmittedAtUtc <= row.DeadlineUtc);

        // Draft work is invisible to a student, so only published assignments can be owed.
        // An empty class list means the caller found no enrollment — nothing was set for them.
        var classIdList = classIds.ToList();
        var setForThem = classIdList.Count == 0
            ? 0
            : await _context.Assignments.CountAsync(
                a => a.Status == AssignmentStatus.Published
                  && classIdList.Contains(a.ClassCourse.ClassId), ct);

        // Clamped: work handed in for a class the student has since left is counted above but
        // is no longer in what their current classes were set.
        return new SubmissionTimelinessStat(
            onTime,
            handedIn.Count - onTime,
            Math.Max(0, setForThem - handedIn.Count));
    }

    // ── Shared reads and bucketing ────────────────────────────────────────────

    /// <summary>
    /// Live students per class. Distinct on the student: a repeated grade is two enrollment
    /// rows for the same (student, class) in different years, and that is one seat.
    /// </summary>
    private async Task<Dictionary<Guid, int>> GetStudentCountsAsync(
        List<Guid> classIds, CancellationToken ct)
    {
        if (classIds.Count == 0)
        {
            return [];
        }

        return await _context.StudentEnrollments
            .Where(e => classIds.Contains(e.ClassId) && !e.Student.IsDeleted)
            .GroupBy(e => e.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Select(e => e.StudentId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);
    }

    /// <summary>
    /// The window as both a filter bound and the first/last day to emit. The bound is the
    /// midnight that starts the first day, in UTC — matching the buckets and the columns.
    /// </summary>
    private (DateTime StartUtc, DateOnly FirstDay, DateOnly LastDay) Window(int trendDays)
    {
        var lastDay = DateOnly.FromDateTime(_clock.UtcNow);
        var firstDay = lastDay.AddDays(-(trendDays - 1));
        return (firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), firstDay, lastDay);
    }

    /// <summary>
    /// One point per day across the whole window, including days with nothing on them — a
    /// line that skipped empty days would compress a quiet week into a single step and read
    /// as steady activity.
    /// </summary>
    private static List<DailyActivityPoint> BuildTrend(
        IReadOnlyList<ActivityRow> rows, DateOnly firstDay, DateOnly lastDay)
    {
        var submitted = new Dictionary<DateOnly, int>();
        var graded = new Dictionary<DateOnly, int>();

        foreach (var row in rows)
        {
            // Pending means uploaded but never submitted, so its timestamp is not a hand-in.
            if (row.Status != SubmissionStatus.Pending && row.SubmittedAtUtc is { } submittedAt)
            {
                Bump(submitted, DateOnly.FromDateTime(submittedAt));
            }

            // A submission moved back to Pending for re-marking keeps the timestamp of the
            // grading that was withdrawn, so the status is what decides whether it counts.
            if (row.Status == SubmissionStatus.Graded && row.ReviewedAtUtc is { } reviewedAt)
            {
                Bump(graded, DateOnly.FromDateTime(reviewedAt));
            }
        }

        var points = new List<DailyActivityPoint>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            points.Add(new DailyActivityPoint(
                day, submitted.GetValueOrDefault(day), graded.GetValueOrDefault(day)));
        }

        return points;

        static void Bump(Dictionary<DateOnly, int> counts, DateOnly day) =>
            counts[day] = counts.GetValueOrDefault(day) + 1;
    }

    /// <summary>
    /// Counts percentages into the bands. Every band is emitted, empty ones included: a
    /// histogram missing its low bands would look like a class with no weak results rather
    /// than one where nobody scored there.
    /// </summary>
    private static List<GradeBandStat> BuildGradeDistribution(IEnumerable<decimal> percentages)
    {
        var counts = new int[GradeBands.Length];

        foreach (var percent in percentages)
        {
            var index = Array.FindIndex(GradeBands, band => percent <= band.UpperInclusive);
            counts[index < 0 ? GradeBands.Length - 1 : index]++;
        }

        return [.. GradeBands.Select((band, index) => new GradeBandStat(band.Band, counts[index]))];
    }

    /// <summary>
    /// Marks as a share of the maximum they were out of — what makes two assignments with
    /// different maximums comparable. One decimal place: the extra digits are noise on an
    /// axis label.
    /// </summary>
    private static decimal Percent(decimal marks, decimal outOf) =>
        outOf <= 0 ? 0m : Math.Round(marks / outOf * 100m, 1);

    /// <summary>Projection shared by the two trend queries, so both bucket identically.</summary>
    private sealed record ActivityRow(DateTime? SubmittedAtUtc, DateTime? ReviewedAtUtc, SubmissionStatus Status);

    private sealed record AssignmentRef(Guid Id, string Title, DateTime DeadlineUtc, Guid ClassId);

    private sealed record GradedRow(
        Guid SubmissionId,
        string AssignmentTitle,
        Guid CourseId,
        string CourseName,
        string CourseCode,
        DateTime ReviewedAtUtc,
        decimal Marks,
        decimal OutOf);
}
