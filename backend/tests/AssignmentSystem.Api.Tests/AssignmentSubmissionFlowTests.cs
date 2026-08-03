using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The happy path an evaluator will walk by hand, driven against the seeded demo
/// accounts: teacher creates → publishes, student submits, teacher grades, student
/// sees the marks.
/// </summary>
public class AssignmentSubmissionFlowTests : IntegrationTestBase
{
    public AssignmentSubmissionFlowTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task EndToEnd_Flow_ShouldSucceed()
    {
        using var teacher = await SignInAsync(DbSeeder.TeacherEmail, DbSeeder.DefaultPassword);
        using var student = await SignInAsync(DbSeeder.StudentEmail, DbSeeder.DefaultPassword);

        // The seeded teacher's own class/subject mapping.
        var teacherAssignmentId = await SeededTeacherAssignmentIdAsync(teacher);

        // 1. Create a draft, then publish it.
        var assignment = await CreateAssignmentAsync(teacher, teacherAssignmentId, "Integration Test Assignment");
        assignment.Status.Should().Be(AssignmentStatus.Draft);

        var publish = await teacher.PostAsync($"/api/v1/assignments/{assignment.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<AssignmentDto>(publish)).Status.Should().Be(AssignmentStatus.Published);

        // 2. The student sees it and submits.
        var visible = await student.GetAsync($"/api/v1/assignments/{assignment.Id}");
        visible.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitResponse = await student.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/submissions",
            new SubmitAssignmentRequest("Here is my text response."));
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var submission = await ReadAsync<SubmissionDto>(submitResponse);
        submission.Status.Should().Be(SubmissionStatus.Submitted);

        // 3. The teacher grades it.
        var grade = await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new ReviewSubmissionRequest(90m, "Excellent working!", SubmissionStatus.Graded));
        grade.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. The student sees marks and feedback.
        var verify = await student.GetAsync($"/api/v1/submissions/{submission.Id}");
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var graded = await ReadAsync<SubmissionDto>(verify);
        graded.Status.Should().Be(SubmissionStatus.Graded);
        graded.Marks.Should().Be(90m);
        graded.MarksOutOf.Should().Be(100m);
        graded.Feedback.Should().Be("Excellent working!");
    }
}
