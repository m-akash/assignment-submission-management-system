using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Features.Dashboard;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The pre-aggregated chart data behind the three overview screens.
///
/// The scoping tests are the important ones: these endpoints hand over other people's numbers
/// in aggregate, which is exactly the shape of leak a per-row authorization check does not
/// catch. Every assertion about totals is made inside a provisioned world — the suite shares
/// one database, so a school-wide count would be whatever other tests happened to leave behind.
/// </summary>
public sealed class DashboardStatsTests : IntegrationTestBase
{
    public DashboardStatsTests(ApiFactory api) : base(api) { }

    // ── Role gating ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EachDashboard_IsReachableOnlyByItsOwnRole()
    {
        var world = await ProvisionWorldAsync("dash-role");
        using var admin = await SignInAsAdminAsync();
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        (await admin.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await teacher.GetAsync("/api/v1/dashboard/teacher")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await student.GetAsync("/api/v1/dashboard/student")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A dashboard is not a "read the school" endpoint that happens to be shaped per role:
        // each one answers from a scope only that role has, so the other two are forbidden.
        (await teacher.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetAsync("/api/v1/dashboard/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/v1/dashboard/teacher")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await student.GetAsync("/api/v1/dashboard/teacher")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/v1/dashboard/student")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.GetAsync("/api/v1/dashboard/student")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_WithoutAToken_IsUnauthorized()
    {
        using var anonymous = Api.CreateClient();
        (await anonymous.GetAsync("/api/v1/dashboard/admin")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── The trend window ──────────────────────────────────────────────────────

    /// <summary>
    /// One point per day across the whole window, days with nothing on them included — a
    /// series that skipped empty days would compress a quiet week into one step and read as
    /// steady activity.
    /// </summary>
    [Fact]
    public async Task AdminTrend_HasOnePointPerDay_EndingToday()
    {
        using var admin = await SignInAsAdminAsync();

        var stats = await ReadAsync<AdminDashboardStats>(
            await admin.GetAsync("/api/v1/dashboard/admin?days=14"));

        stats.ActivityTrend.Should().HaveCount(14);
        stats.ActivityTrend.Select(p => p.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        stats.ActivityTrend[^1].Date.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));

        // Consecutive, not merely sorted: a gap would be an empty day quietly dropped.
        for (var i = 1; i < stats.ActivityTrend.Count; i++)
        {
            stats.ActivityTrend[i].Date.Should().Be(stats.ActivityTrend[i - 1].Date.AddDays(1));
        }
    }

    /// <summary>
    /// An out-of-range window is clamped, not rejected. A dashboard asking for a decade wants
    /// the longest chart it can have; a 422 would leave the panel empty over a query string.
    /// </summary>
    [Theory]
    [InlineData(1, DashboardWindow.MinDays)]
    [InlineData(0, DashboardWindow.MinDays)]
    [InlineData(-5, DashboardWindow.MinDays)]
    [InlineData(5000, DashboardWindow.MaxDays)]
    public async Task AdminTrend_ClampsTheRequestedWindow(int requested, int expected)
    {
        using var admin = await SignInAsAdminAsync();

        var stats = await ReadAsync<AdminDashboardStats>(
            await admin.GetAsync($"/api/v1/dashboard/admin?days={requested}"));

        stats.ActivityTrend.Should().HaveCount(expected);
    }

    /// <summary>The two states are the whole of it, so they add up to every assignment.</summary>
    [Fact]
    public async Task AdminAssignmentStatus_CountsDraftsAndPublishedSeparately()
    {
        var world = await ProvisionWorldAsync("dash-status");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        var before = await ReadAsync<AdminDashboardStats>(
            await admin.GetAsync("/api/v1/dashboard/admin"));

        await CreateAssignmentAsync(teacher, world.ClassCourseId, "Still a draft");
        await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Out in the world");

        var after = await ReadAsync<AdminDashboardStats>(
            await admin.GetAsync("/api/v1/dashboard/admin"));

        // Deltas rather than totals: the suite shares a database with the seeded school.
        (after.AssignmentStatus.Draft - before.AssignmentStatus.Draft).Should().Be(1);
        (after.AssignmentStatus.Published - before.AssignmentStatus.Published).Should().Be(1);
    }

    // ── Teacher scope ─────────────────────────────────────────────────────────

    /// <summary>
    /// The three segments of an assignment's bar are the class it was set for, and they move as
    /// work arrives and is marked. A submission still Pending — files uploaded, never handed in
    /// — counts as not handed in, which is what the teacher is actually waiting on.
    /// </summary>
    [Fact]
    public async Task TeacherProgress_SplitsTheRoster_AndFollowsTheWork()
    {
        var world = await ProvisionWorldAsync("dash-prog");
        using var teacher = await SignInAsync(world.TeacherEmail);

        // A second student, so "the whole roster" is distinguishable from "one student".
        var classmateEmail = await AddStudentToClassAsync(world.ClassId, "dp");
        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Progress Set");

        var initial = await ProgressForAsync(teacher, assignment.Id);
        initial.Graded.Should().Be(0);
        initial.AwaitingMarking.Should().Be(0);
        initial.NotSubmitted.Should().Be(2, "nobody has handed in yet, and the class has two students");

        using var student = await SignInAsync(world.StudentEmail);
        var submission = await SubmitAsync(student, assignment.Id);

        var handedIn = await ProgressForAsync(teacher, assignment.Id);
        handedIn.Graded.Should().Be(0);
        handedIn.AwaitingMarking.Should().Be(1);
        handedIn.NotSubmitted.Should().Be(1);

        // An upload without a hand-in must not move out of "not submitted": the classmate has
        // attached a file but never submitted, so there is still nothing to mark.
        using var classmate = await SignInAsync(classmateEmail);
        await AttachAsync(classmate, assignment.Id, "draft.pdf");

        var withAnUpload = await ProgressForAsync(teacher, assignment.Id);
        withAnUpload.AwaitingMarking.Should().Be(1);
        withAnUpload.NotSubmitted.Should().Be(1);

        var review = await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new ReviewSubmissionRequest(90m, "Good work.", SubmissionStatus.Graded));
        review.EnsureSuccessStatusCode();

        var marked = await ProgressForAsync(teacher, assignment.Id);
        marked.Graded.Should().Be(1);
        marked.AwaitingMarking.Should().Be(0);
        marked.NotSubmitted.Should().Be(1);

        // The three always sum to the roster — that is what makes a bar's length readable as
        // "the class" rather than "however many rows happen to exist".
        (marked.Graded + marked.AwaitingMarking + marked.NotSubmitted).Should().Be(2);
    }

    /// <summary>
    /// A teacher's dashboard is their own authored work — the ownership column rule B3 checks.
    /// Aggregates are exactly where a missing scope hides, so this asserts the absence.
    /// </summary>
    [Fact]
    public async Task TeacherDashboard_ExcludesAnotherTeachersWork()
    {
        var mine = await ProvisionWorldAsync("dash-mine");
        var theirs = await ProvisionWorldAsync("dash-thrs");

        using var myTeacher = await SignInAsync(mine.TeacherEmail);
        using var otherTeacher = await SignInAsync(theirs.TeacherEmail);

        var myAssignment = await CreatePublishedAssignmentAsync(myTeacher, mine.ClassCourseId, "Mine Only");
        var theirAssignment = await CreatePublishedAssignmentAsync(
            otherTeacher, theirs.ClassCourseId, "Theirs Only");

        // Marks given by the other teacher, so the grade spread has something to leak.
        using var theirStudent = await SignInAsync(theirs.StudentEmail);
        var theirSubmission = await SubmitAsync(theirStudent, theirAssignment.Id);
        (await otherTeacher.PostAsJsonAsync(
            $"/api/v1/submissions/{theirSubmission.Id}/review",
            new ReviewSubmissionRequest(50m, null, SubmissionStatus.Graded))).EnsureSuccessStatusCode();

        var stats = await ReadAsync<TeacherDashboardStats>(
            await myTeacher.GetAsync("/api/v1/dashboard/teacher"));

        stats.AssignmentProgress.Should().Contain(a => a.AssignmentId == myAssignment.Id);
        stats.AssignmentProgress.Should().NotContain(a => a.AssignmentId == theirAssignment.Id);
        stats.GradeDistribution.Sum(band => band.Count)
            .Should().Be(0, "this teacher has marked nothing, whatever the other one has done");
    }

    /// <summary>
    /// Marks are banded as a share of what each one was out of, not as raw numbers: an
    /// assignment out of 20 and one out of 100 otherwise land in different bands for the same
    /// performance. Every band is present so an empty low band reads as "nobody scored there".
    /// </summary>
    [Fact]
    public async Task TeacherGradeDistribution_BandsByPercentage_AndKeepsEmptyBands()
    {
        var world = await ProvisionWorldAsync("dash-band");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        // Nine out of ten and ninety out of a hundred are the same result, so both belong in
        // the top band — which is only true if the server divides before it bands.
        var outOfTen = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Out of ten", maxMarks: 10m);
        var outOfHundred = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Out of a hundred");

        foreach (var (assignmentId, marks) in new[] { (outOfTen.Id, 9m), (outOfHundred.Id, 90m) })
        {
            var submission = await SubmitAsync(student, assignmentId);
            (await teacher.PostAsJsonAsync(
                $"/api/v1/submissions/{submission.Id}/review",
                new ReviewSubmissionRequest(marks, null, SubmissionStatus.Graded))).EnsureSuccessStatusCode();
        }

        var stats = await ReadAsync<TeacherDashboardStats>(
            await teacher.GetAsync("/api/v1/dashboard/teacher"));

        stats.GradeDistribution.Should().HaveCount(5);
        stats.GradeDistribution[^1].Band.Should().Be("81-100");
        stats.GradeDistribution[^1].Count.Should().Be(2);
        stats.GradeDistribution.Take(4).Should().OnlyContain(band => band.Count == 0);
    }

    // ── Student scope ─────────────────────────────────────────────────────────

    /// <summary>
    /// A student sees their own marks and nobody else's. Two classmates on the same assignment
    /// is the case that matters: they share a class, an offering and a teacher, so scope here
    /// can only come from the submission's own student id.
    /// </summary>
    [Fact]
    public async Task StudentDashboard_ShowsOnlyTheCallersOwnMarks()
    {
        var world = await ProvisionWorldAsync("dash-self");
        using var teacher = await SignInAsync(world.TeacherEmail);
        var classmateEmail = await AddStudentToClassAsync(world.ClassId, "ds");

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Shared Set");

        using var student = await SignInAsync(world.StudentEmail);
        using var classmate = await SignInAsync(classmateEmail);

        var mine = await SubmitAsync(student, assignment.Id);
        var theirs = await SubmitAsync(classmate, assignment.Id);

        (await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{mine.Id}/review",
            new ReviewSubmissionRequest(30m, null, SubmissionStatus.Graded))).EnsureSuccessStatusCode();
        (await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{theirs.Id}/review",
            new ReviewSubmissionRequest(100m, null, SubmissionStatus.Graded))).EnsureSuccessStatusCode();

        var stats = await ReadAsync<StudentDashboardStats>(
            await student.GetAsync("/api/v1/dashboard/student"));

        stats.MarksOverTime.Should().ContainSingle();
        stats.MarksOverTime[0].SubmissionId.Should().Be(mine.Id);
        stats.MarksOverTime[0].Percent.Should().Be(30m);

        // The average is the caller's own, not the pair's — 65% would mean the classmate's
        // mark had been folded in.
        stats.CourseAverages.Should().ContainSingle();
        stats.CourseAverages[0].AveragePercent.Should().Be(30m);
        stats.CourseAverages[0].GradedCount.Should().Be(1);
    }

    /// <summary>
    /// Work the class was set but the student never handed in. Only published assignments can
    /// be owed: a draft is invisible to them, so counting it would accuse them of missing
    /// something they were never shown.
    /// </summary>
    [Fact]
    public async Task StudentTimeliness_CountsPublishedWorkNotHandedIn_AndIgnoresDrafts()
    {
        var world = await ProvisionWorldAsync("dash-time");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var baseline = await ReadAsync<StudentDashboardStats>(
            await student.GetAsync("/api/v1/dashboard/student"));

        await CreateAssignmentAsync(teacher, world.ClassCourseId, "Never published");
        var published = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, "Owed");

        var owed = await ReadAsync<StudentDashboardStats>(
            await student.GetAsync("/api/v1/dashboard/student"));

        (owed.Timeliness.NotSubmitted - baseline.Timeliness.NotSubmitted)
            .Should().Be(1, "the draft is invisible to the student, so only the published one is owed");
        owed.Timeliness.OnTime.Should().Be(baseline.Timeliness.OnTime);

        await SubmitAsync(student, published.Id);

        var handedIn = await ReadAsync<StudentDashboardStats>(
            await student.GetAsync("/api/v1/dashboard/student"));

        (handedIn.Timeliness.OnTime - baseline.Timeliness.OnTime).Should().Be(1);
        handedIn.Timeliness.NotSubmitted.Should().Be(baseline.Timeliness.NotSubmitted);
        handedIn.Timeliness.Late.Should().Be(baseline.Timeliness.Late, "the deadline is a week away");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>The one assignment's row out of the teacher's progress chart.</summary>
    private static async Task<AssignmentProgressStat> ProgressForAsync(HttpClient teacher, Guid assignmentId)
    {
        var stats = await ReadAsync<TeacherDashboardStats>(
            await teacher.GetAsync("/api/v1/dashboard/teacher"));

        var row = stats.AssignmentProgress.FirstOrDefault(a => a.AssignmentId == assignmentId);
        row.Should().NotBeNull("a published assignment the caller authored belongs on their chart");
        return row!;
    }
}
