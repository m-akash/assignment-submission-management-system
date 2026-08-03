using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Database-level guarantees that the API layer relies on but cannot demonstrate on
/// its own: the one-submission-per-student unique index (X4) and optimistic
/// concurrency via Postgres <c>xmin</c> (X10).
/// </summary>
public class PersistenceConstraintTests : IntegrationTestBase
{
    public PersistenceConstraintTests(ApiFactory api) : base(api) { }

    // ── X4: one submission per student per assignment ─────────────────────────

    [Fact]
    public async Task Resubmitting_ShouldUpdateInPlace_NotCreateASecondSubmission()
    {
        var world = await ProvisionWorldAsync("x4-api");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.TeacherAssignmentId);

        var first = await SubmitAsync(student, assignment.Id, "First attempt.");
        var second = await SubmitAsync(student, assignment.Id, "Second attempt.");

        second.Id.Should().Be(first.Id, "a resubmission updates the existing row");
        second.Content.Should().Be("Second attempt.");

        var listed = await teacher.GetAsync($"/api/v1/submissions?assignmentId={assignment.Id}&pageSize=100");
        var submissions = await ReadAsync<List<SubmissionDto>>(listed);
        submissions.Count(s => s.StudentId == world.StudentId).Should().Be(1);
    }

    [Fact]
    public async Task DuplicateSubmissionRow_ShouldBeRejectedByTheUniqueIndex()
    {
        var world = await ProvisionWorldAsync("x4-db");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var dto = await CreatePublishedAssignmentAsync(teacher, world.TeacherAssignmentId);
        await SubmitAsync(student, dto.Id, "The one and only.");

        // Bypass the handler's "does one already exist?" check and go straight at the table.
        await using var scope = Api.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await context.Assignments.SingleAsync(a => a.Id == dto.Id);

        var duplicate = Submission.Create(
            assignment.Id, world.StudentId, "Sneaky duplicate.", hasFile: false, assignment, SystemUtcClock.Instance);
        context.Submissions.Add(duplicate);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("(assignment_id, student_id) is unique");
    }

    // ── X10: optimistic concurrency ───────────────────────────────────────────

    [Fact]
    public async Task ConcurrentUpdatesToTheSameSubmission_ShouldRaiseAConcurrencyConflict()
    {
        var world = await ProvisionWorldAsync("x10");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var dto = await CreatePublishedAssignmentAsync(teacher, world.TeacherAssignmentId);
        var submission = await SubmitAsync(student, dto.Id, "Original answer.");

        // Two scopes read the same row version, then both try to write it.
        await using var scopeA = Api.CreateScope();
        await using var scopeB = Api.CreateScope();
        var contextA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var contextB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var assignmentA = await contextA.Assignments.SingleAsync(a => a.Id == dto.Id);
        var assignmentB = await contextB.Assignments.SingleAsync(a => a.Id == dto.Id);

        var forA = await contextA.Submissions.SingleAsync(s => s.Id == submission.Id);
        var forB = await contextB.Submissions.SingleAsync(s => s.Id == submission.Id);

        forA.Grade(80m, "Graded by the first marker.", world.TeacherId, assignmentA, SystemUtcClock.Instance);
        forB.Grade(40m, "Graded by the second marker.", world.TeacherId, assignmentB, SystemUtcClock.Instance);

        await contextA.SaveChangesAsync();

        var act = async () => await contextB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "xmin changed underneath the second writer");
    }

    [Fact]
    public async Task StaleWriteAfterAnApiGrade_ShouldRaiseAConcurrencyConflict()
    {
        var world = await ProvisionWorldAsync("x10-api");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var dto = await CreatePublishedAssignmentAsync(teacher, world.TeacherAssignmentId);
        var submission = await SubmitAsync(student, dto.Id, "Original answer.");

        // Hold a copy read before the API writes, then try to commit it afterwards —
        // the shape of two markers grading the same submission at once.
        await using var scope = Api.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await context.Assignments.SingleAsync(a => a.Id == dto.Id);
        var stale = await context.Submissions.SingleAsync(s => s.Id == submission.Id);

        var graded = await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new Api.Controllers.ReviewSubmissionRequest(70m, "Marked via the API.", Domain.Enums.SubmissionStatus.Graded));
        graded.StatusCode.Should().Be(HttpStatusCode.OK);

        stale.Grade(30m, "Stale marker.", world.TeacherId, assignment, SystemUtcClock.Instance);

        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// The Conflict → 409 leg of the error mapping, over real HTTP. Note the deeper
    /// <c>DbUpdateException</c> → 409 fallback in the middleware is unreachable this way
    /// by design: every write handler pre-checks its unique constraints, so a violation
    /// surfaces as a <c>Result</c> failure. It is a safety net, exercised at the DbContext
    /// level by the two tests above.
    /// </summary>
    [Fact]
    public async Task DuplicateTeacherAssignment_ShouldReturn409()
    {
        var world = await ProvisionWorldAsync("dup-ta");
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/teacher-assignments",
            new Api.Controllers.CreateTeacherAssignmentRequest(world.TeacherId, world.SubjectId, world.ClassId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed class SystemUtcClock : IClock
    {
        public static readonly SystemUtcClock Instance = new();
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
