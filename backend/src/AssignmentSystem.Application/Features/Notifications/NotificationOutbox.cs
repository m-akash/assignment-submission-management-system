using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Features.ClassCourses;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.Users;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// Writes the outbox rows. Every method adds and returns without saving — the caller's
/// UnitOfWork commits them alongside the change that caused them (see
/// <see cref="INotificationOutbox"/>).
///
/// It resolves the offering and recipient addresses itself rather than making callers
/// pre-load them: a handler forgetting an <c>Include</c> would otherwise turn into a null
/// reference at the moment of sending, far from the cause.
///
/// Failing to queue must never fail the underlying action. A missing recipient or an
/// offering that has since gone is logged and skipped: a student's submission is not
/// worth rejecting because we could not compose an email about it.
/// </summary>
internal sealed class NotificationOutbox : INotificationOutbox
{
    private readonly IRepository<Notification> _notifications;
    private readonly IRepository<ClassCourse> _classCourses;
    private readonly IRepository<ApplicationUser> _users;
    private readonly IClassRosterRepository _roster;
    private readonly INotificationSettings _settings;
    private readonly ILogger<NotificationOutbox> _logger;

    public NotificationOutbox(
        IRepository<Notification> notifications,
        IRepository<ClassCourse> classCourses,
        IRepository<ApplicationUser> users,
        IClassRosterRepository roster,
        INotificationSettings settings,
        ILogger<NotificationOutbox> logger)
    {
        _notifications = notifications;
        _classCourses = classCourses;
        _users = users;
        _roster = roster;
        _settings = settings;
        _logger = logger;
    }

    public async Task QueueAssignmentPublishedAsync(Assignment assignment, CancellationToken ct = default)
    {
        var offering = await LoadOfferingAsync(assignment, ct);
        if (offering is null)
        {
            return;
        }

        var recipients = await _roster.GetClassRecipientsAsync(offering.ClassId, ct);
        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "Assignment {AssignmentId} published to class {ClassId} with no enrolled students — nothing to notify.",
                assignment.Id, offering.ClassId);
            return;
        }

        foreach (var recipient in recipients)
        {
            var (subject, body) = NotificationMessages.AssignmentPublished(
                assignment, offering, recipient.FullName, _settings.AppBaseUrl);

            await _notifications.AddAsync(
                Notification.Queue(
                    recipient.UserId,
                    recipient.Email,
                    NotificationType.AssignmentPublished,
                    subject,
                    body,
                    assignmentId: assignment.Id),
                ct);
        }

        _logger.LogInformation(
            "Queued {Count} assignment-published notifications for assignment {AssignmentId}.",
            recipients.Count, assignment.Id);
    }

    public async Task QueueSubmissionReceivedAsync(Submission submission, Assignment assignment, CancellationToken ct = default)
    {
        var offering = await LoadOfferingAsync(assignment, ct);
        if (offering is null)
        {
            return;
        }

        var teacher = await _users.GetByIdAsync(assignment.TeacherId, ct);
        if (teacher is null || !teacher.IsActive)
        {
            _logger.LogWarning(
                "Skipping submission-received notification for assignment {AssignmentId}: owning teacher {TeacherId} is missing or inactive.",
                assignment.Id, assignment.TeacherId);
            return;
        }

        var student = await _users.GetByIdAsync(submission.StudentId, ct);
        var studentName = student?.FullName ?? "A student";

        var (subject, body) = NotificationMessages.SubmissionReceived(
            assignment, offering, teacher.FullName, studentName, submission, _settings.AppBaseUrl);

        await _notifications.AddAsync(
            Notification.Queue(
                teacher.Id,
                teacher.EmailValue,
                NotificationType.SubmissionReceived,
                subject,
                body,
                assignmentId: assignment.Id,
                submissionId: submission.Id),
            ct);
    }

    public async Task QueueSubmissionGradedAsync(Submission submission, Assignment assignment, CancellationToken ct = default)
    {
        var offering = await LoadOfferingAsync(assignment, ct);
        if (offering is null)
        {
            return;
        }

        var student = await _users.GetByIdAsync(submission.StudentId, ct);
        if (student is null || !student.IsActive)
        {
            _logger.LogWarning(
                "Skipping submission-graded notification for submission {SubmissionId}: student {StudentId} is missing or inactive.",
                submission.Id, submission.StudentId);
            return;
        }

        var (subject, body) = NotificationMessages.SubmissionGraded(
            assignment, offering, student.FullName, submission, _settings.AppBaseUrl);

        await _notifications.AddAsync(
            Notification.Queue(
                student.Id,
                student.EmailValue,
                NotificationType.SubmissionGraded,
                subject,
                body,
                assignmentId: assignment.Id,
                submissionId: submission.Id),
            ct);
    }

    /// <summary>
    /// The offering with its class and course. Reuses the already-loaded navigation when
    /// the caller happened to include it, so the common paths cost no extra query.
    /// </summary>
    private async Task<ClassCourse?> LoadOfferingAsync(Assignment assignment, CancellationToken ct)
    {
        if (assignment.ClassCourse is { Class: not null, Course: not null } loaded)
        {
            return loaded;
        }

        var spec = new ClassCourseWithDetailsSpecification(assignment.ClassCourseId);
        var offering = await _classCourses.FirstOrDefaultAsync(spec, ct);

        if (offering is null)
        {
            _logger.LogWarning(
                "Skipping notification for assignment {AssignmentId}: offering {ClassCourseId} was not found.",
                assignment.Id, assignment.ClassCourseId);
        }

        return offering;
    }
}
