using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Rules B1 (students see only their own class), B3 (teachers manage only their own
/// assignments) and X3 (drafts are invisible to students), proved by having a second
/// class/teacher/student attempt every cross-boundary operation.
/// </summary>
public class AssignmentAuthorizationTests : IntegrationTestBase
{
    public AssignmentAuthorizationTests(ApiFactory api) : base(api) { }

    // ── B1: class scoping for students ────────────────────────────────────────

    [Fact]
    public async Task Student_CannotReadAssignmentFromAnotherClass()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var outsiderStudent = await SignInAsync(outsider.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);

        var response = await outsiderStudent.GetAsync($"/api/v1/assignments/{assignment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_AssignmentList_ContainsOwnClassOnly()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);
        using var ownerStudent = await SignInAsync(owner.StudentEmail);

        var mine = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId, "Mine");
        var theirs = await CreatePublishedAssignmentAsync(outsiderTeacher, outsider.ClassCourseId, "Theirs");

        var response = await ownerStudent.GetAsync("/api/v1/assignments?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var visible = (await ReadAsync<List<AssignmentDto>>(response)).Select(a => a.Id).ToList();
        visible.Should().Contain(mine.Id);
        visible.Should().NotContain(theirs.Id);
    }

    [Fact]
    public async Task Student_CannotSubmitToAssignmentFromAnotherClass()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var outsiderStudent = await SignInAsync(outsider.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);

        var response = await outsiderStudent.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest("Not my class."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── X3: drafts are not student-visible ────────────────────────────────────

    [Fact]
    public async Task Student_CannotReadDraftAssignment()
    {
        var world = await ProvisionWorldAsync("draft");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId);

        var read = await student.GetAsync($"/api/v1/assignments/{draft.Id}");
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var list = await student.GetAsync("/api/v1/assignments?pageSize=100");
        (await ReadAsync<List<AssignmentDto>>(list)).Select(a => a.Id).Should().NotContain(draft.Id);
    }

    [Fact]
    public async Task Student_CannotSubmitToDraftAssignment()
    {
        var world = await ProvisionWorldAsync("nodraft");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId);

        var response = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{draft.Id}/submissions",
            new SubmitAssignmentRequest("Too early."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── B3: teachers may only manage their own assignments ────────────────────

    [Fact]
    public async Task Teacher_CannotCreateAssignmentAgainstAnotherTeachersMapping()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await outsiderTeacher.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            owner.ClassCourseId,
            "Poaching another teacher's class",
            "Should not be allowed.",
            System.DateTime.UtcNow.AddDays(3),
            50m,
            true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotUpdateAnotherTeachersAssignment()
    {
        var (assignment, outsiderTeacher, _) = await AssignmentWithOutsiderAsync();

        var response = await outsiderTeacher.PutAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}",
            new UpdateAssignmentRequest("Hijacked", "Changed by someone else.", System.DateTime.UtcNow.AddDays(9), 10m, false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotPublishAnotherTeachersAssignment()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var draft = await CreateAssignmentAsync(ownerTeacher, owner.ClassCourseId);

        var response = await outsiderTeacher.PostAsync($"/api/v1/assignments/{draft.Id}/publish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotDeleteAnotherTeachersAssignment()
    {
        var (assignment, outsiderTeacher, _) = await AssignmentWithOutsiderAsync();

        var response = await outsiderTeacher.DeleteAsync($"/api/v1/assignments/{assignment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotGradeSubmissionOnAnotherTeachersAssignment()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var ownerStudent = await SignInAsync(owner.StudentEmail);
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);
        var submission = await SubmitAsync(ownerStudent, assignment.Id, "My work.");

        var response = await outsiderTeacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new ReviewSubmissionRequest(10m, "Not my class.", SubmissionStatus.Graded));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotReadSubmissionOnAnotherTeachersAssignment()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var ownerStudent = await SignInAsync(owner.StudentEmail);
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);
        var submission = await SubmitAsync(ownerStudent, assignment.Id, "My work.");

        var response = await outsiderTeacher.GetAsync($"/api/v1/submissions/{submission.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_SubmissionList_ExcludesOtherTeachersSubmissions()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);
        using var ownerStudent = await SignInAsync(owner.StudentEmail);
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);
        var submission = await SubmitAsync(ownerStudent, assignment.Id, "My work.");

        var mine = await ownerTeacher.GetAsync("/api/v1/submissions?pageSize=100");
        (await ReadAsync<List<SubmissionDto>>(mine)).Select(s => s.Id).Should().Contain(submission.Id);

        var theirs = await outsiderTeacher.GetAsync("/api/v1/submissions?pageSize=100");
        (await ReadAsync<List<SubmissionDto>>(theirs)).Select(s => s.Id).Should().NotContain(submission.Id);
    }

    // ── Student ownership of submissions ──────────────────────────────────────

    [Fact]
    public async Task Student_CannotReadAnotherStudentsSubmission()
    {
        var world = await ProvisionWorldAsync("peers");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var author = await SignInAsync(world.StudentEmail);

        // A classmate: same class, so the assignment is visible — the submission must not be.
        var classmateEmail = await AddStudentToClassAsync(world.ClassId, "peer");
        using var classmate = await SignInAsync(classmateEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var submission = await SubmitAsync(author, assignment.Id, "Private answer.");

        (await classmate.GetAsync($"/api/v1/assignments/{assignment.Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "the classmate is in the same class");

        var response = await classmate.GetAsync($"/api/v1/submissions/{submission.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_CannotUpdateAnotherStudentsSubmission()
    {
        var world = await ProvisionWorldAsync("peerupd");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var author = await SignInAsync(world.StudentEmail);

        var classmateEmail = await AddStudentToClassAsync(world.ClassId, "peer2");
        using var classmate = await SignInAsync(classmateEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var submission = await SubmitAsync(author, assignment.Id, "Private answer.");

        var response = await classmate.PutAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}",
            new UpdateSubmissionRequest("Tampered."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Role gates ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_CannotCreateAssignments()
    {
        var world = await ProvisionWorldAsync("rolegate");
        using var student = await SignInAsync(world.StudentEmail);

        var response = await student.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            world.ClassCourseId, "Nope", "Nope", System.DateTime.UtcNow.AddDays(3), 10m, true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_CannotManageUsers()
    {
        var world = await ProvisionWorldAsync("nouser");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var response = await teacher.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A teacher's class/course mappings are their own business. Without scoping, the
    /// endpoint hands any teacher the school's entire teaching roster — and the ids needed
    /// to aim other requests at colleagues' classes.
    /// </summary>
    [Fact]
    public async Task Teacher_TeacherAssignmentList_ExcludesOtherTeachersMappings()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await outsiderTeacher.GetAsync("/api/v1/teacher-assignments?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var mappings = await ReadAsync<List<TeacherAssignmentRow>>(response);
        mappings.Should().NotBeEmpty("the teacher has a mapping of their own");
        mappings.Should().OnlyContain(m => m.TeacherId == outsider.TeacherId);
        mappings.Should().NotContain(m => m.Id == owner.TeacherAssignmentId);
    }

    [Fact]
    public async Task Admin_TeacherAssignmentList_SeesEveryMapping()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync("/api/v1/teacher-assignments?pageSize=250");

        var mappings = await ReadAsync<List<TeacherAssignmentRow>>(response);
        mappings.Select(m => m.Id).Should().Contain([owner.TeacherAssignmentId, outsider.TeacherAssignmentId]);
    }

    private sealed record TeacherAssignmentRow(Guid Id, Guid TeacherId);

    [Fact]
    public async Task Admin_CanReadAnyAssignmentAndSubmission()
    {
        var world = await ProvisionWorldAsync("adminall");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var submission = await SubmitAsync(student, assignment.Id, "Visible to admin.");

        (await admin.GetAsync($"/api/v1/assignments/{assignment.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/submissions/{submission.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(TestWorld Owner, TestWorld Outsider)> TwoWorldsAsync() =>
        (await ProvisionWorldAsync("own"), await ProvisionWorldAsync("out"));

    /// <summary>A published assignment in one world, plus a signed-in teacher from another.</summary>
    private async Task<(AssignmentDto Assignment, HttpClient OutsiderTeacher, TestWorld Owner)> AssignmentWithOutsiderAsync()
    {
        var (owner, outsider) = await TwoWorldsAsync();
        using var ownerTeacher = await SignInAsync(owner.TeacherEmail);

        var assignment = await CreatePublishedAssignmentAsync(ownerTeacher, owner.ClassCourseId);
        var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        return (assignment, outsiderTeacher, owner);
    }
}
