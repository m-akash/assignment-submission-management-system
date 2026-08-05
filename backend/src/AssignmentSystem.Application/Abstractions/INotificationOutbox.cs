using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Writes notification rows for the events worth emailing about. Every method
/// only <i>adds</i> to the change tracker — the calling handler's
/// <c>IUnitOfWork.SaveChangesAsync</c> commits them in the same transaction as the change
/// that caused them. That is the point: a publish either happens with its emails queued
/// or not at all, and no request ever waits on a mail server.
/// </summary>
public interface INotificationOutbox
{
    /// <summary>
    /// One notification per student enrolled in the assignment's class. A draft becoming
    /// published is the only moment students can first see it, so it is the only moment
    /// worth mailing about.
    /// </summary>
    Task QueueAssignmentPublishedAsync(Assignment assignment, CancellationToken ct = default);

    /// <summary>Tells the owning teacher a submission has arrived to review.</summary>
    Task QueueSubmissionReceivedAsync(Submission submission, Assignment assignment, CancellationToken ct = default);

    /// <summary>Tells the student their marks and feedback are ready.</summary>
    Task QueueSubmissionGradedAsync(Submission submission, Assignment assignment, CancellationToken ct = default);

    /// <summary>
    /// Welcomes a newly created account and carries the single-use link its owner uses to
    /// choose a password. Takes the plaintext token rather than looking one up, because it
    /// exists only in the caller's hand for the length of one transaction — see
    /// <see cref="IPasswordSetupTokenService"/>.
    ///
    /// The one queue method given its recipient rather than resolving it: the user is being
    /// created in this same transaction and cannot be read back yet.
    /// </summary>
    Task QueueAccountCreatedAsync(
        ApplicationUser user, PasswordSetupIssue setup, CancellationToken ct = default);

    /// <summary>
    /// Tells a teacher they now teach a course offering — the moment they gain the right to
    /// create assignments and grade for it, so the moment worth mailing about.
    /// </summary>
    Task QueueTeacherAssignedAsync(TeacherAssignment teacherAssignment, CancellationToken ct = default);

    /// <summary>
    /// Tells a student they are now in a class, listing the courses that class studies.
    ///
    /// Called from both enrollment paths — creating a student with a class, and adding an
    /// existing student to another one — because they are the same event from the student's
    /// side. Mailing only one of them would leave a gap that looks like a bug.
    /// </summary>
    Task QueueStudentEnrolledAsync(StudentEnrollment enrollment, CancellationToken ct = default);
}
