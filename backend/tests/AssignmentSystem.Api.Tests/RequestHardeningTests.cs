using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Shared.Common;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Server-side limits on what a request may ask for: page size is capped, and a
/// submission's attachments come from what is stored rather than what the body claims.
/// </summary>
public class RequestHardeningTests : IntegrationTestBase
{
    public RequestHardeningTests(ApiFactory api) : base(api) { }

    // ── Pagination ceiling ────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/assignments")]
    [InlineData("/api/v1/submissions")]
    [InlineData("/api/v1/classes")]
    [InlineData("/api/v1/courses")]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/teacher-assignments")]
    public async Task ListEndpoints_ShouldCapPageSize(string url)
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"{url}?pageSize=1000000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (items, pagination) = await ReadPageAsync<object>(response);

        pagination.PageSize.Should().Be(PageDefaults.MaxPageSize);
        items.Count.Should().BeLessThanOrEqualTo(PageDefaults.MaxPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ListEndpoints_WithNonsensePageSize_ShouldFallBackToTheDefault(int pageSize)
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/assignments?pageSize={pageSize}");

        var (_, pagination) = await ReadPageAsync<AssignmentDto>(response);
        pagination.PageSize.Should().Be(PageDefaults.DefaultPageSize);
    }

    [Fact]
    public async Task ListEndpoints_WithPageBelowOne_ShouldClampToTheFirstPage()
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync("/api/v1/assignments?page=-3");

        var (_, pagination) = await ReadPageAsync<AssignmentDto>(response);
        pagination.Page.Should().Be(PageDefaults.FirstPage);
    }

    [Fact]
    public async Task ListEndpoints_WithPageSizeAtTheCeiling_ShouldBeHonoured()
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.GetAsync($"/api/v1/assignments?pageSize={PageDefaults.MaxPageSize}");

        var (_, pagination) = await ReadPageAsync<AssignmentDto>(response);
        pagination.PageSize.Should().Be(PageDefaults.MaxPageSize);
    }

    // ── Submission content cannot be faked ────────────────────────────────────

    [Fact]
    public async Task Submit_WithNoTextAndNoUploadedFile_ShouldBeRejected()
    {
        var world = await ProvisionWorldAsync("empty");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        var response = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest(null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// The request contract used to carry <c>fileIds</c>, and a non-empty list was accepted
    /// as proof that the submission had attachments. Any ids — including another student's —
    /// satisfied it, so an empty submission could be created. The field is gone; sending it
    /// changes nothing.
    /// </summary>
    [Fact]
    public async Task Submit_WithFabricatedFileIds_ShouldStillBeRejectedAsEmpty()
    {
        var world = await ProvisionWorldAsync("fabricated");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        // Raw JSON, so the removed property is genuinely on the wire.
        using var body = JsonContent.Create(new
        {
            content = (string?)null,
            fileIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
        });

        var response = await student.PostAsync($"/api/v1/assignments/{assignment.Id}/submissions", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("text answer or a file");
    }

    [Fact]
    public async Task Submit_WithTextOnly_ShouldSucceed()
    {
        var world = await ProvisionWorldAsync("textonly");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        var submission = await SubmitAsync(student, assignment.Id, "Just text, no attachment.");

        submission.Content.Should().Be("Just text, no attachment.");
        submission.Files.Should().BeEmpty();
    }
}
