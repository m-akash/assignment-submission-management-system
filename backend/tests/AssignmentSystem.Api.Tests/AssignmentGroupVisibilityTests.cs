using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.AssignmentFiles;
using AssignmentSystem.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// A course can be restricted to a group (e.g. Physics → Science). That restriction must
/// actually gate what a student can see and do — listing, reading, submitting to, and
/// downloading attachments from an assignment built on such a course — not just sit on
/// the student's profile as a cosmetic fact.
/// </summary>
public class AssignmentGroupVisibilityTests : IntegrationTestBase
{
    public AssignmentGroupVisibilityTests(ApiFactory api) : base(api) { }

    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, .. "-1.7 test"u8];

    [Fact]
    public async Task GetAssignments_ForStudentOutsideCourseGroup_ShouldExcludeIt()
    {
        var scenario = await GroupScenarioAsync("gv-list");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var inGroup = await SignInAsync(scenario.InGroupStudentEmail);
        using var outGroup = await SignInAsync(scenario.OutGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        var (inGroupItems, _) = await ReadPageAsync<AssignmentDto>(await inGroup.GetAsync("/api/v1/assignments"));
        inGroupItems.Should().Contain(a => a.Id == assignment.Id);

        var (outGroupItems, _) = await ReadPageAsync<AssignmentDto>(await outGroup.GetAsync("/api/v1/assignments"));
        outGroupItems.Should().NotContain(a => a.Id == assignment.Id);
    }

    [Fact]
    public async Task GetAssignmentById_ForStudentOutsideCourseGroup_ShouldBeForbidden()
    {
        var scenario = await GroupScenarioAsync("gv-get");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var outGroup = await SignInAsync(scenario.OutGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        var response = await outGroup.GetAsync($"/api/v1/assignments/{assignment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAssignmentById_ForStudentInCourseGroup_ShouldSucceed()
    {
        var scenario = await GroupScenarioAsync("gv-getok");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var inGroup = await SignInAsync(scenario.InGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        var response = await inGroup.GetAsync($"/api/v1/assignments/{assignment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_ByStudentOutsideCourseGroup_ShouldBeForbidden()
    {
        var scenario = await GroupScenarioAsync("gv-sub");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var outGroup = await SignInAsync(scenario.OutGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        var response = await outGroup.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest("An answer."));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_ByStudentInCourseGroup_ShouldSucceed()
    {
        var scenario = await GroupScenarioAsync("gv-subok");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var inGroup = await SignInAsync(scenario.InGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        var response = await inGroup.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest("An answer."));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UploadSubmissionFile_ByStudentOutsideCourseGroup_ShouldBeForbidden()
    {
        var scenario = await GroupScenarioAsync("gv-upl");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var outGroup = await SignInAsync(scenario.OutGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "answer.pdf");

        var response = await outGroup.PostAsync($"/api/v1/assignments/{assignment.Id}/submissions/upload", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DownloadAttachment_ByStudentOutsideCourseGroup_ShouldBeForbidden()
    {
        var scenario = await GroupScenarioAsync("gv-att");
        using var teacher = await SignInAsync(scenario.TeacherEmail);
        using var inGroup = await SignInAsync(scenario.InGroupStudentEmail);
        using var outGroup = await SignInAsync(scenario.OutGroupStudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, scenario.TeacherAssignmentId);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(PdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "handout.pdf");
        var upload = await teacher.PostAsync($"/api/v1/assignments/{assignment.Id}/attachments/upload", form);
        upload.EnsureSuccessStatusCode();
        var uploaded = await ReadAsync<AssignmentFileDto>(upload);

        var forbidden = await outGroup.GetAsync($"/api/v1/assignments/attachments/{uploaded.Id}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var allowed = await inGroup.GetAsync($"/api/v1/assignments/attachments/{uploaded.Id}");
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTeacherAssignment_WithGroupRestrictedCourseInAClassWithoutGroups_ShouldBeRejected()
    {
        var scenario = await GroupScenarioAsync("gv-mismatch");

        var response = await scenario.Admin.PostAsJsonAsync("/api/v1/teacher-assignments",
            new CreateTeacherAssignmentRequest(scenario.TeacherId, scenario.CourseId, scenario.JuniorClassId));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("does not have groups");
    }

    // ── Fixture ───────────────────────────────────────────────────────────────

    private sealed record GroupScenario(
        HttpClient Admin,
        Guid SeniorClassId,
        Guid JuniorClassId,
        Guid CourseId,
        Guid GroupId,
        Guid OtherGroupId,
        Guid TeacherId,
        Guid TeacherAssignmentId,
        string TeacherEmail,
        string InGroupStudentEmail,
        string OutGroupStudentEmail);

    private async Task<GroupScenario> GroupScenarioAsync(string label)
    {
        var tag = $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];
        var admin = await SignInAsAdminAsync();

        var seniorClass = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/classes",
            new CreateClassRequest($"Class {tag} senior", 9, tag)));

        var juniorClass = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/classes",
            new CreateClassRequest($"Class {tag} junior", 8, tag)));

        // Department/group codes are capped at 10 characters, so built from the guid instead.
        var departmentCode = $"D{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var department = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/departments",
            new CreateDepartmentRequest($"Department {tag}", departmentCode)));

        var groupCode = $"G{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var group = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/groups",
            new CreateGroupRequest($"Group {tag}", groupCode)));

        var otherGroupCode = $"H{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var otherGroup = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/groups",
            new CreateGroupRequest($"Other {tag}", otherGroupCode)));

        var course = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/courses",
            new CreateCourseRequest($"Course {tag}", $"CRS-{tag}", department.Id, group.Id)));

        var teacherEmail = $"teacher-{tag}@test.local";
        var teacher = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(teacherEmail, $"Teacher {tag}", TestPassword, Role.Teacher, null, department.Id, null)));

        var inGroupEmail = $"in-{tag}@test.local";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(inGroupEmail, $"InGroup {tag}", TestPassword, Role.Student, seniorClass.Id, null, group.Id)))
            .EnsureSuccessStatusCode();

        var outGroupEmail = $"out-{tag}@test.local";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(outGroupEmail, $"OutGroup {tag}", TestPassword, Role.Student, seniorClass.Id, null, otherGroup.Id)))
            .EnsureSuccessStatusCode();

        var teacherAssignment = await ReadAsync<IdRow>(await admin.PostAsJsonAsync("/api/v1/teacher-assignments",
            new CreateTeacherAssignmentRequest(teacher.Id, course.Id, seniorClass.Id)));

        return new GroupScenario(
            admin, seniorClass.Id, juniorClass.Id, course.Id, group.Id, otherGroup.Id,
            teacher.Id, teacherAssignment.Id, teacherEmail, inGroupEmail, outGroupEmail);
    }

    private sealed record IdRow(Guid Id);
}
