using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The two fields authored as markup — an assignment brief and a student's written answer —
/// end to end: what survives storage, what does not, and what search makes of it.
/// </summary>
public sealed class RichTextContentTests : IntegrationTestBase
{
    public RichTextContentTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task Search_ShouldMatchWordsInADescriptionRatherThanItsTags()
    {
        var world = await ProvisionWorldAsync("rtsearch");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var marker = $"photosynthesis{Guid.NewGuid():N}"[..24];
        await CreateWithDescriptionAsync(
            teacher, world.ClassCourseId, $"<h2>Biology</h2><ul><li>Explain {marker}</li></ul>");

        var byWord = await FindAsync(teacher, marker);
        byWord.Should().HaveCount(1, "the word is in the brief, formatting or not");

        // Each of these is a literal substring of the stored HTML, and each one matched this
        // assignment back when search ran against the markup. None of them is anything a
        // person typed, so none of them should find anything now.
        foreach (var fragment in new[] { "h2>Biology", "ul><li", "li>Explain" })
        {
            var byMarkup = await FindAsync(teacher, fragment);
            byMarkup.Should().BeEmpty($"'{fragment}' is markup, not something anyone wrote");
        }
    }

    [Fact]
    public async Task Search_ShouldStillFindDescriptionsWrittenAsPlainText()
    {
        var world = await ProvisionWorldAsync("rtplain");
        using var teacher = await SignInAsync(world.TeacherEmail);

        // No tags at all — the shape every description had before the editor existed.
        var marker = $"tessellation{Guid.NewGuid():N}"[..22];
        await CreateWithDescriptionAsync(teacher, world.ClassCourseId, $"Read about {marker} tonight.");

        (await FindAsync(teacher, marker)).Should().HaveCount(1);
    }

    [Fact]
    public async Task CreatingAnAssignment_ShouldStoreTheBriefWithoutItsScripts()
    {
        var world = await ProvisionWorldAsync("rtxss");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var created = await CreateWithDescriptionAsync(
            teacher,
            world.ClassCourseId,
            "<p>Read <strong>chapter 4</strong>.</p><script>steal()</script>"
                + "<p onclick=\"steal()\">Then answer.</p>");

        created.Description.Should().NotContain("script").And.NotContain("onclick");
        created.Description.Should().Contain("<strong>chapter 4</strong>", "formatting is the point");
        created.Description.Should().Contain("Then answer.", "only the handler was stripped, not the sentence");
    }

    [Fact]
    public async Task SubmittingAnAnswer_ShouldKeepItsFormattingAndDropItsScripts()
    {
        var world = await ProvisionWorldAsync("rtanswer");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var submission = await SubmitAsync(
            student,
            assignment.Id,
            "<p>My answer:</p><ol><li>First</li></ol><img src=x onerror=\"steal()\">");

        submission.Content.Should().NotContain("onerror").And.NotContain("<img");
        submission.Content.Should().Contain("<ol>").And.Contain("First");
    }

    [Fact]
    public async Task SubmittingAnEmptyEditorWithNoFile_ShouldBeRejected()
    {
        var world = await ProvisionWorldAsync("rtempty");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        // What an editor posts once it has been typed into and cleared again. It is neither
        // null nor whitespace, so nothing short of reading the words inside it can tell that
        // this student has not actually answered.
        var response = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest("<p></p>"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreatingAnAssignmentWithAnEmptyEditor_ShouldBeRejected()
    {
        var world = await ProvisionWorldAsync("rtnodesc");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var response = await teacher.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            world.ClassCourseId,
            "Untitled work",
            "<p><br></p>",
            DateTime.UtcNow.AddDays(7),
            100m,
            true));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AssignmentDto> CreateWithDescriptionAsync(
        HttpClient teacher, Guid classCourseId, string description)
    {
        var response = await teacher.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            classCourseId,
            $"Brief {Guid.NewGuid():N}"[..14],
            description,
            DateTime.UtcNow.AddDays(7),
            100m,
            true));

        response.EnsureSuccessStatusCode();
        return await ReadAsync<AssignmentDto>(response);
    }

    private static async Task<List<AssignmentDto>> FindAsync(HttpClient client, string search)
    {
        var response = await client.GetAsync(
            $"/api/v1/assignments?pageSize=100&search={Uri.EscapeDataString(search)}");

        response.EnsureSuccessStatusCode();
        var (items, _) = await ReadPageAsync<AssignmentDto>(response);
        return items;
    }
}
