using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.AssignmentFiles;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The upload / download / delete matrix for assignment attachments — teacher-uploaded
/// reference material on an assignment, distinct from a student's own submission files.
/// </summary>
public class AssignmentFileAuthorizationTests : IntegrationTestBase
{
    public AssignmentFileAuthorizationTests(ApiFactory api) : base(api) { }

    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, .. "-1.7 test"u8];

    /// <summary>Matches <c>FileStorage:MaxFilesPerAssignment</c> in appsettings.json.</summary>
    private const int MaxFilesPerAssignment = 5;

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ByOwningTeacher_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("aup-ok");

        var response = await UploadAsync(scenario.Teacher, scenario.Assignment.Id, "handout.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await ReadAsync<AssignmentFileDto>(response);
        file.OriginalFileName.Should().Be("handout.pdf");
        file.FileSizeBytes.Should().Be(PdfBytes.Length);
    }

    /// <summary>
    /// Coursework is read-only for admins: attachments are teacher-authored reference material,
    /// so an admin may read but not add them.
    /// </summary>
    [Fact]
    public async Task Upload_ByAdmin_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("aup-adm");
        using var admin = await SignInAsAdminAsync();

        var response = await UploadAsync(admin, scenario.Assignment.Id, "handout.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ByNonOwningTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("aup-out");
        var outsider = await ProvisionWorldAsync("aup-out2");
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await UploadAsync(outsiderTeacher, scenario.Assignment.Id, "handout.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ByStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("aup-stu");

        var response = await UploadAsync(scenario.Student, scenario.Assignment.Id, "handout.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_BeyondTheConfiguredFileCount_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("aup-max");

        for (var i = 1; i <= MaxFilesPerAssignment; i++)
        {
            var allowed = await UploadAsync(scenario.Teacher, scenario.Assignment.Id, $"handout-{i}.pdf", PdfBytes);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"attachment {i} is within the limit");
        }

        var rejected = await UploadAsync(scenario.Teacher, scenario.Assignment.Id, "handout-overflow.pdf", PdfBytes);

        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("at most");
    }

    // ── Download ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Download_ByStudentInClass_ShouldReturnBytes()
    {
        var scenario = await ScenarioAsync("adl-stu");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await scenario.Student.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Download_ByStudentInAnotherClass_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("adl-peer");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var outsider = await ProvisionWorldAsync("adl-peer2");
        using var outsiderStudent = await SignInAsync(outsider.StudentEmail);

        var response = await outsiderStudent.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_WhenAssignmentIsDraft_ByStudent_ShouldBeForbidden()
    {
        var world = await ProvisionWorldAsync("adl-drf");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId);
        var upload = await UploadAsync(teacher, draft.Id, "handout.pdf", PdfBytes);
        upload.EnsureSuccessStatusCode();
        var file = await ReadAsync<AssignmentFileDto>(upload);

        var response = await student.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_ByOwningTeacher_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("adl-tch");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await scenario.Teacher.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ByNonOwningTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("adl-out");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var outsider = await ProvisionWorldAsync("adl-out2");
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await outsiderTeacher.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_ByAdmin_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("adl-adm");
        var file = await UploadFileAsync(scenario, "handout.pdf");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_WithoutAuthentication_ShouldBeUnauthorized()
    {
        var scenario = await ScenarioAsync("adl-anon");
        var file = await UploadFileAsync(scenario, "handout.pdf");
        using var anonymous = Api.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("adel-stu");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await scenario.Student.DeleteAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ByNonOwningTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("adel-out");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var outsider = await ProvisionWorldAsync("adel-out2");
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await outsiderTeacher.DeleteAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ByOwningTeacher_ShouldRemoveFileAndMakeItUndownloadable()
    {
        var scenario = await ScenarioAsync("adel-own");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var delete = await scenario.Teacher.DeleteAsync($"/api/v1/assignments/attachments/{file.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var download = await scenario.Teacher.GetAsync($"/api/v1/assignments/attachments/{file.Id}");
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Coursework is read-only for admins: an admin may read attachments but not delete
    /// another teacher's reference material.
    /// </summary>
    [Fact]
    public async Task Delete_ByAdmin_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("adel-adm");
        var file = await UploadFileAsync(scenario, "handout.pdf");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.DeleteAsync($"/api/v1/assignments/attachments/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_ByOwningTeacher_ShouldRelabelWithoutTouchingTheBytes()
    {
        var scenario = await ScenarioAsync("arn-own");
        var file = await UploadFileAsync(scenario, "IMG_20240817_113044.pdf");

        var response = await RenameAsync(scenario.Teacher, file.Id, "Week 3 worksheet");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await ReadAsync<AssignmentFileDto>(response);
        renamed.OriginalFileName.Should().Be("Week 3 worksheet.pdf");

        var download = await scenario.Teacher.GetAsync($"/api/v1/assignments/attachments/{file.Id}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    /// <summary>
    /// The extension describes the bytes, which were checked against it at upload. A
    /// rename may change the label and nothing else — otherwise renaming would be a way
    /// around the signature check the upload had to pass.
    /// </summary>
    [Fact]
    public async Task Rename_ToAnotherExtension_ShouldKeepTheStoredOne()
    {
        var scenario = await ScenarioAsync("arn-ext");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await RenameAsync(scenario.Teacher, file.Id, "payload.exe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<AssignmentFileDto>(response)).OriginalFileName.Should().Be("payload.pdf");
    }

    [Fact]
    public async Task Rename_WithPathComponents_ShouldKeepOnlyTheFileName()
    {
        var scenario = await ScenarioAsync("arn-path");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await RenameAsync(scenario.Teacher, file.Id, "../../etc/passwd");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<AssignmentFileDto>(response)).OriginalFileName.Should().Be("passwd.pdf");
    }

    [Fact]
    public async Task Rename_WithABlankName_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("arn-blank");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await RenameAsync(scenario.Teacher, file.Id, "   ");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rename_ByNonOwningTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("arn-out");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var outsider = await ProvisionWorldAsync("arn-out2");
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await RenameAsync(outsiderTeacher, file.Id, "mine now");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rename_ByStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("arn-stu");
        var file = await UploadFileAsync(scenario, "handout.pdf");

        var response = await RenameAsync(scenario.Student, file.Id, "not my material");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Coursework stays read-only for admins, renaming included.</summary>
    [Fact]
    public async Task Rename_ByAdmin_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("arn-adm");
        var file = await UploadFileAsync(scenario, "handout.pdf");
        using var admin = await SignInAsAdminAsync();

        var response = await RenameAsync(admin, file.Id, "tidier name");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record Scenario(TestWorld World, AssignmentDto Assignment, HttpClient Teacher, HttpClient Student);

    private async Task<Scenario> ScenarioAsync(string label)
    {
        var world = await ProvisionWorldAsync(label);
        var teacher = await SignInAsync(world.TeacherEmail);
        var student = await SignInAsync(world.StudentEmail);
        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        return new Scenario(world, assignment, teacher, student);
    }

    private async Task<AssignmentFileDto> UploadFileAsync(Scenario scenario, string fileName)
    {
        var response = await UploadAsync(scenario.Teacher, scenario.Assignment.Id, fileName, PdfBytes);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<AssignmentFileDto>(response);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid assignmentId, string fileName, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync($"/api/v1/assignments/{assignmentId}/attachments/upload", form);
    }

    private static async Task<HttpResponseMessage> RenameAsync(HttpClient client, Guid fileId, string fileName) =>
        await client.PutAsJsonAsync(
            $"/api/v1/assignments/attachments/{fileId}",
            new Api.Common.RenameFileRequest(fileName));
}
