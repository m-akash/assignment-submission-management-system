using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Features.ClassCourses;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
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
    private readonly IRepository<Class> _classes;
    private readonly IRepository<ApplicationUser> _users;
    private readonly IClassRosterRepository _roster;
    private readonly INotificationSettings _settings;
    private readonly ILogger<NotificationOutbox> _logger;

    public NotificationOutbox(
        IRepository<Notification> notifications,
        IRepository<ClassCourse> classCourses,
        IRepository<Class> classes,
        IRepository<ApplicationUser> users,
        IClassRosterRepository roster,
        INotificationSettings settings,
        ILogger<NotificationOutbox> logger)
    {
        _notifications = notifications;
        _classCourses = classCourses;
        _classes = classes;
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
            assignment, offering, teacher.FullName, studentName, submission, _settings.AppBaseUrl,
            studentIdNumber: student?.StudentId);

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

    public async Task QueueAccountCreatedAsync(
        ApplicationUser user, PasswordSetupIssue setup, CancellationToken ct = default)
    {
        var (subject, body) = NotificationMessages.AccountCreated(
            user, setup.Token, setup.ExpiresAtUtc, _settings.AppBaseUrl);

        await _notifications.AddAsync(
            Notification.Queue(
                user.Id,
                user.EmailValue,
                NotificationType.AccountCreated,
                subject,
                body),
            ct);
    }

    public async Task QueueTeacherAssignedAsync(TeacherAssignment teacherAssignment, CancellationToken ct = default)
    {
        var offering = await LoadOfferingAsync(teacherAssignment.ClassCourseId, ct);
        if (offering is null)
        {
            _logger.LogWarning(
                "Skipping teacher-assigned notification for mapping {TeacherAssignmentId}: offering {ClassCourseId} was not found.",
                teacherAssignment.Id, teacherAssignment.ClassCourseId);
            return;
        }

        var teacher = await _users.GetByIdAsync(teacherAssignment.TeacherId, ct);
        if (teacher is null || !teacher.IsActive)
        {
            _logger.LogWarning(
                "Skipping teacher-assigned notification for mapping {TeacherAssignmentId}: teacher {TeacherId} is missing or inactive.",
                teacherAssignment.Id, teacherAssignment.TeacherId);
            return;
        }

        // Roster size at the moment of assignment — a teacher gaining a course is naturally
        // curious how many students that means grading for, and it costs one grouped query
        // this class already has the repository for.
        var studentCounts = await _roster.GetStudentCountsAsync([offering.ClassId], ct);
        var enrolledStudentCount = studentCounts.GetValueOrDefault(offering.ClassId, 0);

        var (subject, body) = NotificationMessages.TeacherAssignedToCourse(
            offering, teacher.FullName, _settings.AppBaseUrl,
            teacherIdNumber: teacher.TeacherId, enrolledStudentCount: enrolledStudentCount);

        await _notifications.AddAsync(
            Notification.Queue(
                teacher.Id,
                teacher.EmailValue,
                NotificationType.TeacherAssignedToCourse,
                subject,
                body),
            ct);
    }

    public async Task QueueStudentEnrolledAsync(StudentEnrollment enrollment, CancellationToken ct = default)
    {
        // Find, not a spec query: on the create-user path the student row is Added but not yet
        // saved, and FindAsync answers from the change tracker without touching the database.
        var student = await _users.GetByIdAsync(enrollment.StudentId, ct);
        if (student is null || !student.IsActive)
        {
            _logger.LogWarning(
                "Skipping enrollment notification for enrollment {EnrollmentId}: student {StudentId} is missing or inactive.",
                enrollment.Id, enrollment.StudentId);
            return;
        }

        var @class = enrollment.Class ?? await _classes.GetByIdAsync(enrollment.ClassId, ct);
        if (@class is null)
        {
            _logger.LogWarning(
                "Skipping enrollment notification for enrollment {EnrollmentId}: class {ClassId} was not found.",
                enrollment.Id, enrollment.ClassId);
            return;
        }

        // The courses the class studies, so the student learns what they are taking in the
        // same mail. An empty list is a valid answer — the message says so explicitly.
        var offerings = await _classCourses.ListAsync(
            new ClassCourseOfferingsForClassSpecification(enrollment.ClassId), ct);
        var courses = offerings.Select(o => o.Course).ToList();

        // Read before this enrollment's row has been committed (the caller saves after
        // queueing), so it counts the students already there — precisely "classmates",
        // not "everyone including yourself".
        var studentCounts = await _roster.GetStudentCountsAsync([enrollment.ClassId], ct);
        var classmateCount = studentCounts.GetValueOrDefault(enrollment.ClassId, 0);

        var (subject, body) = NotificationMessages.StudentEnrolled(
            @class, courses, student.FullName, student.StudentId, _settings.AppBaseUrl,
            classmateCount: classmateCount);

        await _notifications.AddAsync(
            Notification.Queue(
                student.Id,
                student.EmailValue,
                NotificationType.StudentEnrolled,
                subject,
                body),
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

        var offering = await LoadOfferingAsync(assignment.ClassCourseId, ct);

        if (offering is null)
        {
            _logger.LogWarning(
                "Skipping notification for assignment {AssignmentId}: offering {ClassCourseId} was not found.",
                assignment.Id, assignment.ClassCourseId);
        }

        return offering;
    }

    /// <summary>By id, for the callers that hold an offering reference rather than an assignment.</summary>
    private Task<ClassCourse?> LoadOfferingAsync(Guid classCourseId, CancellationToken ct) =>
        _classCourses.FirstOrDefaultAsync(new ClassCourseWithDetailsSpecification(classCourseId), ct);
}
