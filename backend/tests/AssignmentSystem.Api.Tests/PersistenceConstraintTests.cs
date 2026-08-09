using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Application.Features.Submissions;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
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

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

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

        var dto = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
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

        var dto = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
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

        var dto = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
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
            new Api.Controllers.CreateTeacherAssignmentRequest(world.TeacherId, world.ClassCourseId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// One teacher per offering: a second, different teacher cannot be mapped to an offering
    /// that already has one, even though the (teacher, offering) pair itself is unique — the
    /// unique index on the teacher_assignments table now keys on ClassCourseId alone.
    /// </summary>
    [Fact]
    public async Task SecondDifferentTeacherOnSameOffering_ShouldReturn409()
    {
        var world = await ProvisionWorldAsync("second-ta");
        using var admin = await SignInAsAdminAsync();

        var secondTeacherEmail = $"second-teacher-{Guid.NewGuid():N}@test.local";
        var secondTeacherResponse = await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(secondTeacherEmail, "Second Teacher", TestPassword, Role.Teacher, null, null));
        secondTeacherResponse.EnsureSuccessStatusCode();
        var secondTeacher = await ReadAsync<CreatedUserRef>(secondTeacherResponse);

        var response = await admin.PostAsJsonAsync("/api/v1/teacher-assignments",
            new Api.Controllers.CreateTeacherAssignmentRequest(secondTeacher.Id, world.ClassCourseId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── One class per (grade, section) ────────────────────────────────────────

    /// <summary>
    /// A grade may hold any number of sections, but the same section twice is not a class —
    /// it is the same class entered twice. The comparison ignores case, so "a" cannot slip
    /// past an existing "A".
    /// </summary>
    [Fact]
    public async Task SecondClassInTheSameGradeAndSection_ShouldReturn409()
    {
        using var admin = await SignInAsAdminAsync();
        var section = $"S{Guid.NewGuid():N}"[..9];

        var first = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(7, section));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(7, section.ToUpperInvariant()));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MoreSectionsWithinTheSameGrade_ShouldBeAllowed()
    {
        using var admin = await SignInAsAdminAsync();
        var tag = $"{Guid.NewGuid():N}"[..6];

        var a = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(5, $"{tag}-A"));
        var b = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(5, $"{tag}-B"));

        a.StatusCode.Should().Be(HttpStatusCode.Created);
        b.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await ReadAsync<ClassNameRef>(a);
        created.Name.Should().Be($"Class V - Section {tag}-A", "the name is composed, not supplied");
    }

    /// <summary>
    /// The self-collision case: saving a class without moving it must not trip the duplicate
    /// check against the class's own row.
    /// </summary>
    [Fact]
    public async Task UpdatingAClassWithoutChangingItsSlot_ShouldSucceed()
    {
        using var admin = await SignInAsAdminAsync();
        var section = $"S{Guid.NewGuid():N}"[..9];

        var created = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(4, section));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var klass = await ReadAsync<ClassNameRef>(created);

        var updated = await admin.PutAsJsonAsync($"/api/v1/classes/{klass.Id}", new UpdateClassRequest(4, section));

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MovingAClassOntoAnOccupiedSlot_ShouldReturn409()
    {
        using var admin = await SignInAsAdminAsync();
        var tag = $"{Guid.NewGuid():N}"[..6];

        var occupied = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(3, $"{tag}-A"));
        occupied.StatusCode.Should().Be(HttpStatusCode.Created);

        var mover = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(3, $"{tag}-B"));
        mover.StatusCode.Should().Be(HttpStatusCode.Created);
        var klass = await ReadAsync<ClassNameRef>(mover);

        var response = await admin.PutAsJsonAsync($"/api/v1/classes/{klass.Id}", new UpdateClassRequest(3, $"{tag}-A"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreatingAClassWithoutASection_ShouldBeRejected()
    {
        using var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/classes", new CreateClassRequest(6, "  "));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private sealed record ClassNameRef(Guid Id, string Name);

    private sealed record CreatedUserRef(Guid Id);

    private sealed class SystemUtcClock : IClock
    {
        public static readonly SystemUtcClock Instance = new();
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
