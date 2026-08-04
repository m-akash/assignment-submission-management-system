using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Domain.Enums;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The two junctions the model now hangs off: course offerings (which courses a class
/// studies) and enrollments (which classes a student is in).
///
/// These are the rules that used to be implicit in a pair of columns, so they are the ones
/// most worth pinning down: an offering is unique and cannot be pulled out from under live
/// work, and a student's visibility follows their enrollments rather than a single class id.
/// </summary>
public sealed class EnrollmentAndOfferingTests : IntegrationTestBase
{
    public EnrollmentAndOfferingTests(ApiFactory api) : base(api) { }

    // ── Offerings ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingDuplicateOffering_Returns409()
    {
        var world = await ProvisionWorldAsync("off-dup");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/class-courses",
            new CreateClassCourseRequest(world.ClassId, world.CourseId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Removing an offering that still has assignments would orphan student work, so it is
    /// refused with an explanation rather than cascading or surfacing as a 500.
    /// </summary>
    [Fact]
    public async Task DeletingOfferingWithAssignments_Returns409()
    {
        var world = await ProvisionWorldAsync("off-inuse");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        var response = await admin.DeleteAsync($"/api/v1/class-courses/{world.ClassCourseId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// With no assignments yet, the teaching mapping is still a dependant — the admin has to
    /// unwind it deliberately, in order.
    /// </summary>
    [Fact]
    public async Task DeletingOfferingWithOnlyTeachingMapping_Returns409_ThenSucceedsOnceRemoved()
    {
        var world = await ProvisionWorldAsync("off-unwind");
        using var admin = await SignInAsAdminAsync();

        var blocked = await admin.DeleteAsync($"/api/v1/class-courses/{world.ClassCourseId}");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var removeMapping = await admin.DeleteAsync($"/api/v1/teacher-assignments/{world.TeacherAssignmentId}");
        removeMapping.IsSuccessStatusCode.Should().BeTrue();

        var allowed = await admin.DeleteAsync($"/api/v1/class-courses/{world.ClassCourseId}");
        allowed.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task CreatingOffering_IsAdminOnly()
    {
        var world = await ProvisionWorldAsync("off-role");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var response = await teacher.PostAsJsonAsync("/api/v1/class-courses",
            new CreateClassCourseRequest(world.ClassId, world.CourseId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Teachers may read the offering list — their assignment form needs it to pick a scope —
    /// but students have no use for the school's catalogue.
    /// </summary>
    [Fact]
    public async Task OfferingList_IsReadableByTeachers_ButNotStudents()
    {
        var world = await ProvisionWorldAsync("off-read");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        (await teacher.GetAsync("/api/v1/class-courses")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await student.GetAsync("/api/v1/class-courses")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A teacher can only set work for an offering they are mapped to. Without this, an
    /// offering id from the (teacher-readable) catalogue would be enough to set work for any
    /// class in the school — rule B3.
    /// </summary>
    [Fact]
    public async Task Teacher_CannotCreateAssignmentForOfferingTheyDoNotTeach()
    {
        var mine = await ProvisionWorldAsync("off-mine");
        var theirs = await ProvisionWorldAsync("off-theirs");
        using var teacher = await SignInAsync(mine.TeacherEmail);

        var response = await teacher.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            theirs.ClassCourseId, "Not mine", "Nope", DateTime.UtcNow.AddDays(3), 10m, true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Coursework is read-only for admins: even though they administer the school, they may
    /// not author assignments — only the teacher who owns the work can create, publish or grade it.
    /// </summary>
    [Fact]
    public async Task Admin_CannotCreateAssignments()
    {
        var world = await ProvisionWorldAsync("off-admin");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            world.ClassCourseId, "Admin draft", "Should not be allowed.", DateTime.UtcNow.AddDays(3), 10m, true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Enrollments ───────────────────────────────────────────────────────────

    /// <summary>Creating a student with a class enrols them, in the same transaction.</summary>
    [Fact]
    public async Task CreatingStudentWithClass_CreatesTheirEnrollment()
    {
        var world = await ProvisionWorldAsync("enr-create");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/enrollments?studentId={world.StudentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (rows, _) = await ReadPageAsync<EnrollmentRow>(response);
        rows.Should().ContainSingle().Which.ClassId.Should().Be(world.ClassId);
    }

    [Fact]
    public async Task EnrollingTheSameStudentTwiceInAClass_Returns409()
    {
        var world = await ProvisionWorldAsync("enr-dup");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, world.ClassId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A student with no class sees no assignments at all, which reads as data loss rather
    /// than an intended state — so their last enrollment cannot be removed.
    /// </summary>
    [Fact]
    public async Task RemovingAStudentsOnlyEnrollment_Returns409()
    {
        var world = await ProvisionWorldAsync("enr-last");
        using var admin = await SignInAsAdminAsync();

        var enrollmentId = await OnlyEnrollmentIdAsync(admin, world.StudentId);

        var response = await admin.DeleteAsync($"/api/v1/enrollments/{enrollmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Moving a student: enrol in the new class first, then remove the old one. The second
    /// delete succeeds because it is no longer their last.
    /// </summary>
    [Fact]
    public async Task MovingAStudent_AddThenRemove_Succeeds()
    {
        var from = await ProvisionWorldAsync("enr-from");
        var to = await ProvisionWorldAsync("enr-to");
        using var admin = await SignInAsAdminAsync();

        var originalEnrollmentId = await OnlyEnrollmentIdAsync(admin, from.StudentId);

        var added = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(from.StudentId, to.ClassId));
        added.IsSuccessStatusCode.Should().BeTrue();

        var removed = await admin.DeleteAsync($"/api/v1/enrollments/{originalEnrollmentId}");
        removed.IsSuccessStatusCode.Should().BeTrue();

        var remaining = await admin.GetAsync($"/api/v1/enrollments?studentId={from.StudentId}");
        var (rows, _) = await ReadPageAsync<EnrollmentRow>(remaining);
        rows.Should().ContainSingle().Which.ClassId.Should().Be(to.ClassId);
    }

    /// <summary>
    /// Enrollment decides what a student can see, so it is exactly what a student must not be
    /// able to change for themselves. Writing enrollments stays admin-only. Teachers may now
    /// read enrollments — but only for classes they teach (see
    /// <see cref="TeacherReadsEnrollments_ScopedToTheirOwnClasses"/>).
    /// </summary>
    [Fact]
    public async Task Enrollments_WritesAreAdminOnly_StudentsForbidden()
    {
        var world = await ProvisionWorldAsync("enr-role");
        using var student = await SignInAsync(world.StudentEmail);

        // A student can neither read nor write enrollments.
        (await student.GetAsync("/api/v1/enrollments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var selfEnrol = await student.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, world.ClassId));
        selfEnrol.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A teacher may read enrollments, but the server scopes them to the classes they teach:
    /// they see their own class's students and are forbidden from naming another teacher's
    /// class. A teacher also cannot write enrollments.
    /// </summary>
    [Fact]
    public async Task TeacherReadsEnrollments_ScopedToTheirOwnClasses()
    {
        var mine = await ProvisionWorldAsync("enr-t-mine");
        var theirs = await ProvisionWorldAsync("enr-t-theirs");
        using var teacher = await SignInAsync(mine.TeacherEmail);

        // Their own class: 200 and contains their student.
        var own = await teacher.GetAsync($"/api/v1/enrollments?classId={mine.ClassId}");
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        var (ownRows, _) = await ReadPageAsync<EnrollmentRow>(own);
        ownRows.Should().ContainSingle().Which.StudentId.Should().Be(mine.StudentId);

        // Another teacher's class: forbidden, not a silent empty page.
        var cross = await teacher.GetAsync($"/api/v1/enrollments?classId={theirs.ClassId}");
        cross.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Unfiltered, they still only see their own class's student.
        var all = await teacher.GetAsync("/api/v1/enrollments");
        var (allRows, _) = await ReadPageAsync<EnrollmentRow>(all);
        allRows.Should().OnlyContain(r => r.ClassId == mine.ClassId);

        // Writing is still admin-only.
        var write = await teacher.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(mine.StudentId, mine.ClassId));
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A teacher is not a student and cannot be enrolled — the roster would then count them
    /// as one and they would be mailed as one.
    /// </summary>
    [Fact]
    public async Task EnrollingATeacher_IsRejected()
    {
        var world = await ProvisionWorldAsync("enr-teach");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.TeacherId, world.ClassId));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule B1 through the new junction: a second enrollment immediately widens what the
    /// student can see, without them signing in again. This is why enrollment is read per
    /// request instead of being carried in the access token.
    /// </summary>
    [Fact]
    public async Task EnrollingIntoASecondClass_ImmediatelyRevealsThatClassesAssignments()
    {
        var home = await ProvisionWorldAsync("b1-home");
        var extra = await ProvisionWorldAsync("b1-extra");
        using var extraTeacher = await SignInAsync(extra.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        var otherClassAssignment = await CreatePublishedAssignmentAsync(extraTeacher, extra.ClassCourseId, "Elective");

        // Signed in once, before the enrollment — the same token is used throughout.
        using var student = await SignInAsync(home.StudentEmail);

        var beforeEnrolment = await student.GetAsync($"/api/v1/assignments/{otherClassAssignment.Id}");
        beforeEnrolment.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var enrol = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(home.StudentId, extra.ClassId));
        enrol.IsSuccessStatusCode.Should().BeTrue();

        var afterEnrolment = await student.GetAsync($"/api/v1/assignments/{otherClassAssignment.Id}");
        afterEnrolment.StatusCode.Should().Be(HttpStatusCode.OK);

        // And it now shows up in their list, alongside their home class's work.
        var list = await student.GetAsync("/api/v1/assignments?pageSize=100");
        var (items, _) = await ReadPageAsync<AssignmentDto>(list);
        items.Should().Contain(a => a.Id == otherClassAssignment.Id);
    }

    /// <summary>
    /// The class filter on the user list is an EXISTS over enrollments now, so a student in
    /// two classes correctly appears under either.
    /// </summary>
    [Fact]
    public async Task UserList_FilteredByClass_FindsStudentsInEachOfTheirClasses()
    {
        var first = await ProvisionWorldAsync("ul-first");
        var second = await ProvisionWorldAsync("ul-second");
        using var admin = await SignInAsAdminAsync();

        var enrol = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(first.StudentId, second.ClassId));
        enrol.IsSuccessStatusCode.Should().BeTrue();

        foreach (var classId in new[] { first.ClassId, second.ClassId })
        {
            var response = await admin.GetAsync($"/api/v1/users?classId={classId}&pageSize=100");
            var (users, _) = await ReadPageAsync<UserRow>(response);
            users.Should().Contain(u => u.Id == first.StudentId, $"the student is enrolled in {classId}");
        }
    }

    /// <summary>The class student count follows enrollments, not a column on the user.</summary>
    [Fact]
    public async Task ClassStudentCount_ReflectsEnrollments()
    {
        var world = await ProvisionWorldAsync("cnt");
        using var admin = await SignInAsAdminAsync();

        await AddStudentToClassAsync(world.ClassId, "cnt2");

        var response = await admin.GetAsync($"/api/v1/classes/{world.ClassId}");
        response.EnsureSuccessStatusCode();

        var @class = await ReadAsync<ClassRow>(response);
        @class.StudentCount.Should().Be(2);
    }

    private async Task<Guid> OnlyEnrollmentIdAsync(HttpClient admin, Guid studentId)
    {
        var response = await admin.GetAsync($"/api/v1/enrollments?studentId={studentId}");
        response.EnsureSuccessStatusCode();

        var (rows, _) = await ReadPageAsync<EnrollmentRow>(response);
        rows.Should().ContainSingle("a freshly provisioned student sits in exactly one class");
        return rows[0].Id;
    }

    private sealed record EnrollmentRow(Guid Id, Guid StudentId, Guid ClassId, string ClassName);
    private sealed record UserRow(Guid Id, Role Role);
    private sealed record ClassRow(Guid Id, string Name, int StudentCount);
}
