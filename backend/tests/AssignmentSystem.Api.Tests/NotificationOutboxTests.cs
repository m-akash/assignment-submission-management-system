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
    /// Being given a course to teach is the moment a teacher gains the right to set and grade
    /// work for it, so it is mailed. The body has to name the course and class — a mail saying
    /// only "you have a new assignment to teach" would send the teacher to the UI to find out
    /// which one.
    /// </summary>
    [Fact]
    public async Task AssigningTeacherToOffering_QueuesNotificationForThatTeacher()
    {
        // ProvisionWorldAsync maps the teacher to the offering itself, so the mail is already
        // queued by the time it returns — no second mapping call needed.
        var world = await ProvisionWorldAsync("notif-ta");
        using var admin = await SignInAsAdminAsync();

        var queued = await NotificationsForRecipientAsync(admin, world.TeacherId);
        var assigned = queued.Where(n => n.Type == NotificationType.TeacherAssignedToCourse).ToList();

        assigned.Should().ContainSingle();
        assigned[0].RecipientId.Should().Be(world.TeacherId);
        assigned[0].Status.Should().Be(NotificationStatus.Pending);
        assigned[0].Body.Should().Contain("Course notif-ta");
        // The class name is composed from grade and section, so the world's tag lands in the section.
        assigned[0].Body.Should().Contain("Section notif-ta");

        // No assignment or submission exists yet — the context ids must stay null rather than
        // being filled with something incidental.
        assigned[0].AssignmentId.Should().BeNull();
        assigned[0].SubmissionId.Should().BeNull();
    }

    /// <summary>
    /// Only the teacher who was mapped. An offering's students have no interest in who was
    /// given it to teach, and the other world's teacher must not hear about it at all.
    /// </summary>
    [Fact]
    public async Task AssigningTeacherToOffering_DoesNotNotifyAnyoneElse()
    {
        var world = await ProvisionWorldAsync("notif-ta2");
        var otherWorld = await ProvisionWorldAsync("notif-ta3");
        using var admin = await SignInAsAdminAsync();

        var studentMail = await NotificationsForRecipientAsync(admin, world.StudentId);
        studentMail.Should().NotContain(n => n.Type == NotificationType.TeacherAssignedToCourse);

        var otherTeacherMail = await NotificationsForRecipientAsync(admin, otherWorld.TeacherId);
        otherTeacherMail
            .Where(n => n.Type == NotificationType.TeacherAssignedToCourse)
            .Should().OnlyContain(n => !n.Body.Contains("notif-ta2", StringComparison.Ordinal));
    }

    /// <summary>
    /// Creating a student with a class enrols them, so it mails them — and the body lists the
    /// courses that class studies, since "which subjects am I taking?" is the actual question.
    /// </summary>
    [Fact]
    public async Task CreatingStudentWithAClass_QueuesEnrollmentNotificationListingTheCourses()
    {
        var world = await ProvisionWorldAsync("notif-enr");
        using var admin = await SignInAsAdminAsync();

        var queued = await NotificationsForRecipientAsync(admin, world.StudentId);
        var enrolled = queued.Where(n => n.Type == NotificationType.StudentEnrolled).ToList();

        enrolled.Should().ContainSingle();
        enrolled[0].Subject.Should().Contain("Section notif-enr");
        enrolled[0].Body.Should().Contain("Course notif-enr");
        enrolled[0].AssignmentId.Should().BeNull();
    }

    /// <summary>
    /// The other enrollment path: an existing student added to a second class. Mails once
    /// more, about the new class — otherwise being moved between classes would be silent.
    /// </summary>
    [Fact]
    public async Task EnrollingAnExistingStudentInAnotherClass_QueuesASecondNotification()
    {
        var world = await ProvisionWorldAsync("notif-enr2");
        var otherWorld = await ProvisionWorldAsync("notif-enr3");
        using var admin = await SignInAsAdminAsync();

        var before = (await NotificationsForRecipientAsync(admin, world.StudentId))
            .Count(n => n.Type == NotificationType.StudentEnrolled);
        before.Should().Be(1, "creating the student with a class already mailed them once");

        var enrol = await admin.PostAsJsonAsync("/api/v1/enrollments",
            new CreateEnrollmentRequest(world.StudentId, otherWorld.ClassId));
        enrol.IsSuccessStatusCode.Should().BeTrue();

        var enrolled = (await NotificationsForRecipientAsync(admin, world.StudentId))
            .Where(n => n.Type == NotificationType.StudentEnrolled)
            .ToList();

        enrolled.Should().HaveCount(2);
        enrolled.Should().ContainSingle(n => n.Subject.Contains("notif-enr3", StringComparison.Ordinal));
    }

    /// <summary>
    /// A teacher is not enrolled in anything, so creating one queues no enrollment mail. The
    /// two branches share a handler, and this is the one that must stay quiet.
    /// </summary>
    [Fact]
    public async Task CreatingATeacher_QueuesNoEnrollmentNotification()
    {
        var world = await ProvisionWorldAsync("notif-noenr");
        using var admin = await SignInAsAdminAsync();

        var teacherMail = await NotificationsForRecipientAsync(admin, world.TeacherId);

        teacherMail.Should().NotContain(n => n.Type == NotificationType.StudentEnrolled);
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

        // Swept until this assignment's rows are drained rather than once: the suite shares a
        // database and every other test is queueing mail into the same outbox, so the batch
        // (capped at 200 server-side, oldest-first) is not guaranteed to reach these rows in
        // one pass. The property under test is that a sweep drains what it takes, not that one
        // sweep happens to be big enough.
        var afterSweep = await DrainOutboxAsync(admin, assignment.Id);

        afterSweep.Should().NotBeEmpty();
        afterSweep.Should().OnlyContain(n => n.Status == NotificationStatus.Sent);
        afterSweep.Should().OnlyContain(n => n.SentAtUtc != null);
        afterSweep.Should().OnlyContain(n => n.AttemptCount == 1);
    }

    /// <summary>
    /// Sweeps until nothing for the given assignment is Pending, then returns its rows.
    /// Bounded so a genuine failure to drain fails the test instead of looping forever.
    /// </summary>
    private async Task<List<NotificationRow>> DrainOutboxAsync(HttpClient admin, Guid assignmentId)
    {
        const int maxSweeps = 20;

        for (var sweep = 0; sweep < maxSweeps; sweep++)
        {
            var rows = await NotificationsForAssignmentAsync(admin, assignmentId);
            if (rows.Count > 0 && rows.TrueForAll(n => n.Status != NotificationStatus.Pending))
            {
                return rows;
            }

            var dispatch = await admin.PostAsync("/api/v1/notifications/dispatch?batchSize=200", null);
            dispatch.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var final = await NotificationsForAssignmentAsync(admin, assignmentId);
        final.Should().NotContain(
            n => n.Status == NotificationStatus.Pending,
            $"the outbox should drain within {maxSweeps} sweeps");

        return final;
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
    /// Deleting hides a row from the outbox (soft delete). The row is gone from the list and
    /// the summary count drops — without anything being physically removed.
    /// </summary>
    [Fact]
    public async Task DeleteSingle_AsAdmin_HidesRowAndRefreshesCount()
    {
        var world = await ProvisionWorldAsync("notif-del");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var queued = await NotificationsForAssignmentAsync(admin, assignment.Id);
        var target = queued.Should().ContainSingle().Subject;

        var summaryResponse = await admin.GetAsync("/api/v1/notifications/summary");
        var summaryBefore = await ReadAsync<NotificationSummary>(summaryResponse);
        summaryBefore.Pending.Should().BeGreaterThanOrEqualTo(1);

        var delete = await admin.DeleteAsync($"/api/v1/notifications/{target.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        // The row no longer appears in the list — the global query filter hid it.
        var remaining = await NotificationsForAssignmentAsync(admin, assignment.Id);
        remaining.Should().BeEmpty("a deleted notification is hidden from reads");

        // ...and a second delete reports the row as gone (still filtered out → NotFound).
        var secondDelete = await admin.DeleteAsync($"/api/v1/notifications/{target.Id}");
        secondDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSingle_AsStudent_IsForbidden()
    {
        var world = await ProvisionWorldAsync("nauth");
        using var student = await SignInAsync(world.StudentEmail);

        var response = await student.DeleteAsync($"/api/v1/notifications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Bulk delete hides every requested row in one call, reports how many it hid, and
    /// silently skips ids that no longer exist (or were already deleted).
    /// </summary>
    [Fact]
    public async Task DeleteBulk_AsAdmin_HidesAllAndCounts()
    {
        var world = await ProvisionWorldAsync("notif-bulk");
        using var teacher = await SignInAsync(world.TeacherEmail);
        using var admin = await SignInAsAdminAsync();

        await AddStudentToClassAsync(world.ClassId, "bd");
        var assignment = await CreatePublishedAssignmentAsync(teacher, world.ClassCourseId);
        var queued = await NotificationsForAssignmentAsync(admin, assignment.Id);
        queued.Should().HaveCountGreaterThanOrEqualTo(2);

        var ids = queued.Select(n => n.Id).ToList();
        var response = await admin.PostAsJsonAsync(
            "/api/v1/notifications/bulk-delete",
            new { ids });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await ReadAsync<BulkDeleteResponse>(response);
        result.Deleted.Should().Be(ids.Count);

        // A non-existent id is tolerated — it simply isn't counted.
        var mixed = ids.Append(Guid.NewGuid()).ToList();
        var repeat = await admin.PostAsJsonAsync(
            "/api/v1/notifications/bulk-delete",
            new { ids = mixed });
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        var repeatResult = await ReadAsync<BulkDeleteResponse>(repeat);
        repeatResult.Deleted.Should().Be(0, "the rows were already hidden on the first call");
    }

    [Fact]
    public async Task DeleteBulk_OverFiveHundred_IsRejected()
    {
        var world = await ProvisionWorldAsync("ncap");
        using var admin = await SignInAsAdminAsync();

        var tooMany = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList();
        var response = await admin.PostAsJsonAsync(
            "/api/v1/notifications/bulk-delete",
            new { ids = tooMany });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
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

    /// <summary>
    /// Notifications addressed to one user. The enrollment and teacher-assigned rows carry no
    /// assignment id — they are about a class and an offering, neither of which the outbox
    /// stores a context column for — so the recipient is what identifies them in a shared
    /// database. Filtered server-side, which the admin role is allowed to do.
    /// </summary>
    private async Task<List<NotificationRow>> NotificationsForRecipientAsync(HttpClient admin, Guid recipientId)
    {
        var response = await admin.GetAsync($"/api/v1/notifications?recipientId={recipientId}&pageSize=200");
        response.EnsureSuccessStatusCode();

        var (rows, _) = await ReadPageAsync<NotificationRow>(response);
        return rows;
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

    // Shape mirrors NotificationSummaryDto / BulkDeleteResult, deserialised from the envelope.
    private sealed record NotificationSummary(int Pending, int Sent, int Failed);
    private sealed record BulkDeleteResponse(int Deleted);
}
