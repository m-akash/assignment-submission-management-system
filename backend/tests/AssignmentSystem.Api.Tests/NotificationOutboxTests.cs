using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The notification outbox: which events queue mail, who it goes to, and what happens when
/// delivery is attempted.
///
/// Asserts on rows rather than on sent email, which is the whole reason the outbox exists.
/// The dispatcher is disabled in <see cref="ApiFactory"/>, so rows stay Pending until a test
/// asks for a sweep — no timer can race these assertions.
/// </summary>
public sealed class NotificationOutboxTests : IntegrationTestBase
{
    public NotificationOutboxTests(ApiFactory api) : base(api) { }

    /// <summary>
    /// Publishing is the moment students can first see the work, so it is the moment the
    /// class is mailed — one notification per enrolled student, and none for a draft.
    /// </summary>
    [Fact]
    public async Task PublishingAssignment_QueuesOneNotificationPerEnrolledStudent()
    {
        var world = await ProvisionWorldAsync("notif-pub");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        // Two students in the class, so "one each" is distinguishable from "one in total".
        await AddStudentToClassAsync(world.ClassId, "np");

        var draft = await CreateAssignmentAsync(teacher, world.ClassCourseId, "Notify Me");
        var beforePublish = await NotificationsForAssignmentAsync(admin, draft.Id);
        beforePublish.Should().BeEmpty("a draft is invisible to students, so there is nothing to announce");

        var publish = await teacher.PostAsync($"/api/v1/assignments/{draft.Id}/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        var queued = await NotificationsForAssignmentAsync(admin, draft.Id);
        queued.Should().HaveCount(2);
        queued.Should().OnlyContain(n => n.Type == NotificationType.AssignmentPublished);
        queued.Should().OnlyContain(n => n.Status == NotificationStatus.Pending);
        queued.Select(n => n.RecipientId).Should().OnlyHaveUniqueItems();
        queued.Should().OnlyContain(n => n.Subject.Contains("Notify Me", StringComparison.Ordinal));
    }

    /// <summary>
    /// A student in a different class must not be mailed. This is rule B1 showing up in the
    /// notification path: the recipient list comes from enrollments, not from "all students".
    /// </summary>
    [Fact]
    public async Task PublishingAssignment_DoesNotNotifyStudentsOfOtherClasses()
    {
        var world = await ProvisionWorldAsync("notif-scope");
        var otherWorld = await ProvisionWorldAsync("notif-other");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        var queued = await NotificationsForAssignmentAsync(admin, assignment.Id);
        queued.Should().ContainSingle().Which.RecipientId.Should().Be(world.StudentId);
        queued.Should().NotContain(n => n.RecipientId == otherWorld.StudentId);
    }

    /// <summary>The teacher who owns the assignment hears about work arriving.</summary>
    [Fact]
    public async Task Submitting_QueuesNotificationForOwningTeacher()
    {
        var world = await ProvisionWorldAsync("notif-sub");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        await SubmitAsync(student, assignment.Id, "Here is my work.");

        var received = (await NotificationsForAssignmentAsync(admin, assignment.Id))
            .Where(n => n.Type == NotificationType.SubmissionReceived)
            .ToList();

        received.Should().ContainSingle();
        received[0].RecipientId.Should().Be(world.TeacherId);
        received[0].SubmissionId.Should().NotBeNull();
    }

    /// <summary>
    /// Editing a submission before the deadline must not re-notify. A student saving three
    /// times should not put three "submission received" emails in the teacher's inbox.
    /// </summary>
    [Fact]
    public async Task ResubmittingBeforeDeadline_DoesNotQueueASecondNotification()
    {
        var world = await ProvisionWorldAsync("notif-resub");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        await SubmitAsync(student, assignment.Id, "First attempt.");
        await SubmitAsync(student, assignment.Id, "Second attempt, still before the deadline.");
        await SubmitAsync(student, assignment.Id, "Third attempt.");

        var received = (await NotificationsForAssignmentAsync(admin, assignment.Id))
            .Count(n => n.Type == NotificationType.SubmissionReceived);

        received.Should().Be(1);
    }

    /// <summary>Grading tells the student their marks are ready — and carries them in the body.</summary>
    [Fact]
    public async Task Grading_QueuesNotificationForStudentWithMarks()
    {
        var world = await ProvisionWorldAsync("notif-grade");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId, maxMarks: 50m);
        var submission = await SubmitAsync(student, assignment.Id, "My answer.");

        var review = await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new ReviewSubmissionRequest(42m, "Good work.", SubmissionStatus.Graded));
        review.StatusCode.Should().Be(HttpStatusCode.OK);

        var graded = (await NotificationsForAssignmentAsync(admin, assignment.Id))
            .Where(n => n.Type == NotificationType.SubmissionGraded)
            .ToList();

        graded.Should().ContainSingle();
        graded[0].RecipientId.Should().Be(world.StudentId);
        graded[0].Body.Should().Contain("42");
        graded[0].Body.Should().Contain("Good work.");
    }

    /// <summary>
    /// Moving a submission back to Pending for re-evaluation (rule B7) is bookkeeping, not
    /// news — mailing it would tell the student marks are ready just as they were withdrawn.
    /// </summary>
    [Fact]
    public async Task ChangingStatusWithoutGrading_QueuesNoNotification()
    {
        var world = await ProvisionWorldAsync("notif-status");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var submission = await SubmitAsync(student, assignment.Id, "My answer.");

        var review = await teacher.PostAsJsonAsync(
            $"/api/v1/submissions/{submission.Id}/review",
            new ReviewSubmissionRequest(0m, null, SubmissionStatus.Pending));
        review.StatusCode.Should().Be(HttpStatusCode.OK);

        var graded = (await NotificationsForAssignmentAsync(admin, assignment.Id))
            .Count(n => n.Type == NotificationType.SubmissionGraded);

        graded.Should().Be(0);
    }

    /// <summary>
    /// With no SMTP host configured — the default for a fresh checkout — a sweep still drains
    /// the queue, logging each message instead of sending it. Rows must end up Sent rather
    /// than accumulating forever: there is nothing to retry against.
    /// </summary>
    [Fact]
    public async Task Dispatch_WithoutSmtpConfigured_MarksQueuedNotificationsSent()
    {
        var world = await ProvisionWorldAsync("notif-drain");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        (await NotificationsForAssignmentAsync(admin, assignment.Id))
            .Should().OnlyContain(n => n.Status == NotificationStatus.Pending);

        var dispatch = await admin.PostAsync("/api/v1/notifications/dispatch?batchSize=200", null);
        dispatch.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSweep = await NotificationsForAssignmentAsync(admin, assignment.Id);
        afterSweep.Should().NotBeEmpty();
        afterSweep.Should().OnlyContain(n => n.Status == NotificationStatus.Sent);
        afterSweep.Should().OnlyContain(n => n.SentAtUtc != null);
        afterSweep.Should().OnlyContain(n => n.AttemptCount == 1);
    }

    /// <summary>
    /// A student may read their own notifications but not the school's outbound queue. The
    /// list endpoint is open to any signed-in user precisely because it scopes them.
    /// </summary>
    [Fact]
    public async Task NotificationList_AsStudent_ShowsOnlyTheirOwnMail()
    {
        var world = await ProvisionWorldAsync("notif-priv");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);

        var response = await student.GetAsync("/api/v1/notifications?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (mine, _) = await ReadPageAsync<NotificationRow>(response);
        mine.Should().NotBeEmpty("the student was notified about the published assignment");
        mine.Should().OnlyContain(n => n.RecipientId == world.StudentId);
    }

    /// <summary>
    /// The recipientId filter must not become a way to read someone else's mail — a student
    /// asking for the teacher's notifications is still scoped back to their own.
    /// </summary>
    [Fact]
    public async Task NotificationList_AsStudentFilteringByAnotherUser_StillScopesToSelf()
    {
        var world = await ProvisionWorldAsync("notif-esc");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var student = await SignInAsync(world.StudentEmail);

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        await SubmitAsync(student, assignment.Id, "Work.");

        var response = await student.GetAsync(
            $"/api/v1/notifications?recipientId={world.TeacherId}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (rows, _) = await ReadPageAsync<NotificationRow>(response);
        rows.Should().NotContain(n => n.RecipientId == world.TeacherId);
    }

    [Fact]
    public async Task NotificationSummary_IsAdminOnly()
    {
        var world = await ProvisionWorldAsync("notif-adm");
        using var teacher = await SignInAsync(world.TeacherEmail);

        var response = await teacher.GetAsync("/api/v1/notifications/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dispatch_IsAdminOnly()
    {
        var world = await ProvisionWorldAsync("notif-disp");
        using var student = await SignInAsync(world.StudentEmail);

        var response = await student.PostAsync("/api/v1/notifications/dispatch", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Notifications for one assignment. Filtered client-side on assignmentId because the
    /// suite shares a database and every other test is queueing mail into the same outbox.
    /// </summary>
    private async Task<List<NotificationRow>> NotificationsForAssignmentAsync(HttpClient admin, Guid assignmentId)
    {
        var response = await admin.GetAsync("/api/v1/notifications?pageSize=200");
        response.EnsureSuccessStatusCode();

        var (rows, _) = await ReadPageAsync<NotificationRow>(response);
        return rows.Where(n => n.AssignmentId == assignmentId).ToList();
    }

    private sealed record NotificationRow(
        Guid Id,
        Guid RecipientId,
        string RecipientEmail,
        NotificationType Type,
        string Subject,
        string Body,
        NotificationStatus Status,
        int AttemptCount,
        DateTime? SentAtUtc,
        string? LastError,
        Guid? AssignmentId,
        Guid? SubmissionId);
}
