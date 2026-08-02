using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Xunit;
using FluentAssertions;

namespace AssignmentSystem.Api.Tests;

public class AssignmentSubmissionFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task EndToEnd_Flow_ShouldSucceed()
    {
        // 1. Authenticate as Teacher
        await AuthenticateAsync(DbSeeder.TeacherEmail, DbSeeder.DefaultPassword);

        // 2. Fetch Teacher Assignments to get a valid TeacherAssignmentId
        var taResponse = await _client.GetAsync("/api/v1/teacher-assignments");
        taResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var taPayload = await taResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<List<TeacherAssignmentDto>>>(JsonOptions);
        taPayload!.Data.Should().NotBeNullOrEmpty();
        var teacherAssignmentId = taPayload.Data![0].Id;

        // 3. Create a draft assignment
        var deadline = DateTime.UtcNow.AddDays(7);
        var createResponse = await _client.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(
            teacherAssignmentId,
            "Integration Test Assignment",
            "Solve all equations.",
            deadline,
            100m,
            true));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdAssignment = await createResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<AssignmentDto>>(JsonOptions);
        var assignmentId = createdAssignment!.Data!.Id;

        // 4. Publish the assignment
        var publishResponse = await _client.PostAsync($"/api/v1/assignments/{assignmentId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Authenticate as Student
        await AuthenticateAsync(DbSeeder.StudentEmail, DbSeeder.DefaultPassword);

        // 6. View Assignments for class
        var viewAssignmentsResponse = await _client.GetAsync("/api/v1/assignments");
        viewAssignmentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. Submit response
        var submitResponse = await _client.PostAsJsonAsync($"/api/v1/assignments/{assignmentId}/submissions", new SubmitAssignmentRequest(
            "Here is my text response.",
            null));
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await submitResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<SubmissionDto>>(JsonOptions);
        var submissionId = submission!.Data!.Id;

        // 8. Authenticate as Teacher
        await AuthenticateAsync(DbSeeder.TeacherEmail, DbSeeder.DefaultPassword);

        // 9. Grade the submission
        var gradeResponse = await _client.PostAsJsonAsync($"/api/v1/submissions/{submissionId}/review", new ReviewSubmissionRequest(
            90m,
            "Excellent working!",
            SubmissionStatus.Graded));
        gradeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Authenticate as Student again
        await AuthenticateAsync(DbSeeder.StudentEmail, DbSeeder.DefaultPassword);

        // 11. Verify grades
        var verifyResponse = await _client.GetAsync($"/api/v1/submissions/{submissionId}");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var verifiedSubmission = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<SubmissionDto>>(JsonOptions);
        verifiedSubmission!.Data!.Status.Should().Be(SubmissionStatus.Graded);
        verifiedSubmission.Data.Marks.Should().Be(90m);
        verifiedSubmission.Data.Feedback.Should().Be("Excellent working!");
    }

    private sealed record TeacherAssignmentDto(Guid Id);
}
