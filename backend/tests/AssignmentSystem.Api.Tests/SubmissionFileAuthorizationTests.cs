using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The upload / download / delete matrix for submission attachments — the most
/// security-sensitive surface in the API, since it moves bytes across role boundaries.
/// </summary>
public class SubmissionFileAuthorizationTests : IntegrationTestBase
{
    public SubmissionFileAuthorizationTests(ApiFactory api) : base(api) { }

    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, .. "-1.7 test"u8];

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ByOwningStudent_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("up-ok");

        var response = await UploadAsync(scenario.Student, scenario.Assignment.Id, "answer.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await ReadAsync<SubmissionFileDto>(response);
        file.OriginalFileName.Should().Be("answer.pdf");
        file.FileSizeBytes.Should().Be(PdfBytes.Length);
    }

    [Fact]
    public async Task Upload_ByStudentFromAnotherClass_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("up-cls");
        var outsider = await ProvisionWorldAsync("up-out");
        using var outsiderStudent = await SignInAsync(outsider.StudentEmail);

        var response = await UploadAsync(outsiderStudent, scenario.Assignment.Id, "answer.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ByTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("up-tch");

        var response = await UploadAsync(scenario.Teacher, scenario.Assignment.Id, "answer.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ToDraftAssignment_ShouldBeForbidden()
    {
        var world = await ProvisionWorldAsync("up-drf");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId);

        var response = await UploadAsync(student, draft.Id, "answer.pdf", PdfBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_WithDisallowedExtension_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("up-ext");

        var response = await UploadAsync(scenario.Student, scenario.Assignment.Id, "payload.exe", "MZ nope"u8.ToArray());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Upload_WhenContentDoesNotMatchExtension_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("up-magic");

        // A ZIP/OOXML header (PK) wearing a .pdf name — the Content-Type header is not trusted.
        var response = await UploadAsync(
            scenario.Student, scenario.Assignment.Id, "disguised.pdf", [0x50, 0x4B, 0x03, 0x04, 0x00]);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Download ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Download_ByOwningStudent_ShouldReturnBytes()
    {
        var scenario = await ScenarioAsync("dl-own");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var response = await scenario.Student.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PdfBytes);
    }

    [Fact]
    public async Task Download_ByOwningTeacher_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("dl-tch");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var response = await scenario.Teacher.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ByAdmin_ShouldSucceed()
    {
        var scenario = await ScenarioAsync("dl-adm");
        var file = await UploadFileAsync(scenario, "answer.pdf");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Download_ByAnotherStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("dl-peer");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var classmateEmail = await AddStudentToClassAsync(scenario.World.ClassId, "dlpeer");
        using var classmate = await SignInAsync(classmateEmail);

        var response = await classmate.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_ByNonOwningTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("dl-out");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var outsider = await ProvisionWorldAsync("dl-o2");
        using var outsiderTeacher = await SignInAsync(outsider.TeacherEmail);

        var response = await outsiderTeacher.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Download_WithoutAuthentication_ShouldBeUnauthorized()
    {
        var scenario = await ScenarioAsync("dl-anon");
        var file = await UploadFileAsync(scenario, "answer.pdf");
        using var anonymous = Api.CreateClient();

        var response = await anonymous.GetAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByAnotherStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("del-peer");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var classmateEmail = await AddStudentToClassAsync(scenario.World.ClassId, "delpeer");
        using var classmate = await SignInAsync(classmateEmail);

        var response = await classmate.DeleteAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ByTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("del-tch");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var response = await scenario.Teacher.DeleteAsync($"/api/v1/submissions/files/{file.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ByOwner_ShouldRemoveFileAndMakeItUndownloadable()
    {
        var scenario = await ScenarioAsync("del-own");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        // A text answer keeps the submission non-empty once the attachment is gone.
        await SubmitAsync(scenario.Student, scenario.Assignment.Id, "Text answer plus a file.");

        var delete = await scenario.Student.DeleteAsync($"/api/v1/submissions/files/{file.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var download = await scenario.Student.GetAsync($"/api/v1/submissions/files/{file.Id}");
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_LastFile_WhenNoTextAnswer_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("del-last");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        // Finalise the submission with the uploaded file as its only content — no text.
        var submit = await scenario.Student.PostAsJsonAsync(
            $"/api/v1/assignments/{scenario.Assignment.Id}/submissions",
            new Api.Controllers.SubmitAssignmentRequest(null));
        submit.EnsureSuccessStatusCode();

        var delete = await scenario.Student.DeleteAsync($"/api/v1/submissions/files/{file.Id}");

        delete.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_ByOwningStudent_ShouldRelabelWithoutTouchingTheBytes()
    {
        var scenario = await ScenarioAsync("rn-own");
        var file = await UploadFileAsync(scenario, "IMG_20240817_113044.pdf");

        var response = await RenameAsync(scenario.Student, file.Id, "Question 3 working");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renamed = await ReadAsync<SubmissionFileDto>(response);
        renamed.OriginalFileName.Should().Be("Question 3 working.pdf");

        var download = await scenario.Student.GetAsync($"/api/v1/submissions/files/{file.Id}");
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
        var scenario = await ScenarioAsync("rn-ext");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var response = await RenameAsync(scenario.Student, file.Id, "payload.exe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<SubmissionFileDto>(response)).OriginalFileName.Should().Be("payload.pdf");
    }

    [Fact]
    public async Task Rename_ByAnotherStudent_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("rn-peer");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var classmateEmail = await AddStudentToClassAsync(scenario.World.ClassId, "rnpeer");
        using var classmate = await SignInAsync(classmateEmail);

        var response = await RenameAsync(classmate, file.Id, "actually mine");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rename_ByTeacher_ShouldBeForbidden()
    {
        var scenario = await ScenarioAsync("rn-tch");
        var file = await UploadFileAsync(scenario, "answer.pdf");

        var response = await RenameAsync(scenario.Teacher, file.Id, "tidier name");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Marked work is settled. Renaming is allowed for exactly as long as adding or
    /// removing a file is, so what the teacher graded keeps the names they saw.
    /// </summary>
    [Fact]
    public async Task Rename_AfterGrading_ShouldBeRejected()
    {
        var scenario = await ScenarioAsync("rn-grd");
        var file = await UploadFileAsync(scenario, "answer.pdf");
        var submission = await SubmitAsync(scenario.Student, scenario.Assignment.Id, "My answer.");

        var grade = await scenario.Teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new Api.Controllers.ReviewSubmissionRequest(90m, "Good work", Domain.Enums.SubmissionStatus.Graded));
        grade.EnsureSuccessStatusCode();

        var response = await RenameAsync(scenario.Student, file.Id, "second thoughts");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
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

    private async Task<SubmissionFileDto> UploadFileAsync(Scenario scenario, string fileName)
    {
        var response = await UploadAsync(scenario.Student, scenario.Assignment.Id, fileName, PdfBytes);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<SubmissionFileDto>(response);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid assignmentId, string fileName, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync($"/api/v1/assignments/{assignmentId}/submissions/upload", form);
    }

    private static async Task<HttpResponseMessage> RenameAsync(HttpClient client, Guid fileId, string fileName) =>
        await client.PutAsJsonAsync(
            $"/api/v1/submissions/files/{fileId}",
            new Api.Common.RenameFileRequest(fileName));
}
