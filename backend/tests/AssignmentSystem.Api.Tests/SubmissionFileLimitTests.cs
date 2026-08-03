using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The configured upload limits, enforced end to end: attachments per submission, file
/// size, and the content type served on download. These settings existed in
/// <c>FileStorage</c> configuration but were previously not read by anything.
/// </summary>
public class SubmissionFileLimitTests : IntegrationTestBase
{
    public SubmissionFileLimitTests(ApiFactory api) : base(api) { }

    /// <summary>Matches <c>FileStorage:MaxFilesPerSubmission</c> in appsettings.json.</summary>
    private const int MaxFilesPerSubmission = 3;

    private static byte[] Pdf(int totalBytes = 32)
    {
        var bytes = new byte[totalBytes];
        "%PDF-1.7"u8.CopyTo(bytes);
        return bytes;
    }

    [Fact]
    public async Task Upload_BeyondTheConfiguredFileCount_ShouldBeRejected()
    {
        var (assignment, student) = await ScenarioAsync("maxfiles");

        for (var i = 1; i <= MaxFilesPerSubmission; i++)
        {
            var allowed = await UploadAsync(student, assignment.Id, $"answer-{i}.pdf", Pdf());
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"attachment {i} is within the limit");
        }

        var rejected = await UploadAsync(student, assignment.Id, "answer-4.pdf", Pdf());

        rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("at most");
    }

    [Fact]
    public async Task Upload_OverTheConfiguredSizeLimit_ShouldBeRejectedWithTheLimitStated()
    {
        var (assignment, student) = await ScenarioAsync("maxbytes");

        // Just past FileStorage:MaxBytes (10 MB) but inside the multipart framing headroom,
        // so the policy answers rather than the server truncating the request.
        var oversized = Pdf((10 * 1024 * 1024) + 1024);

        var response = await UploadAsync(student, assignment.Id, "huge.pdf", oversized);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("10 MB");
    }

    /// <summary>
    /// The stored content type must come from the verified bytes, not the client's header.
    /// Echoing a client-chosen type back on download lets an uploader decide how the
    /// browser renders the file — a stored-XSS route.
    /// </summary>
    [Fact]
    public async Task Upload_ShouldIgnoreTheClientContentType_AndServeTheDerivedOne()
    {
        var (assignment, student) = await ScenarioAsync("mime");
        var text = "plain notes, honestly"u8.ToArray();

        var upload = await UploadAsync(student, assignment.Id, "notes.txt", text, declaredContentType: "text/html");
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        var file = await ReadAsync<SubmissionFileDto>(upload);
        file.ContentType.Should().Be("text/plain", "derived from the .txt extension");

        var download = await student.GetAsync($"/api/v1/submissions/files/{file.Id}");
        download.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Upload_ShouldServeAttachmentDispositionWithTheOriginalName()
    {
        var (assignment, student) = await ScenarioAsync("disposition");

        var upload = await UploadAsync(student, assignment.Id, "my essay.pdf", Pdf());
        var file = await ReadAsync<SubmissionFileDto>(upload);

        var download = await student.GetAsync($"/api/v1/submissions/files/{file.Id}");

        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        download.Content.Headers.ContentDisposition.FileName.Should().Contain("my essay.pdf");
    }

    [Fact]
    public async Task Upload_ShouldStripDirectoryComponentsFromTheFileName()
    {
        var (assignment, student) = await ScenarioAsync("pathname");

        var upload = await UploadAsync(student, assignment.Id, "answer.pdf", Pdf());
        var file = await ReadAsync<SubmissionFileDto>(upload);

        file.OriginalFileName.Should().NotContain("/").And.NotContain("\\");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(AssignmentDto Assignment, HttpClient Student)> ScenarioAsync(string label)
    {
        var world = await ProvisionWorldAsync(label);
        using var teacher = await SignInAsync(world.TeacherEmail);
        var student = await SignInAsync(world.StudentEmail);
        var assignment = await CreatePublishedAssignmentAsync(teacher, world.TeacherAssignmentId);

        return (assignment, student);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid assignmentId,
        string fileName,
        byte[] bytes,
        string declaredContentType = "application/octet-stream")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(declaredContentType);
        form.Add(file, "file", fileName);

        return await client.PostAsync($"/api/v1/assignments/{assignmentId}/submissions/upload", form);
    }
}
