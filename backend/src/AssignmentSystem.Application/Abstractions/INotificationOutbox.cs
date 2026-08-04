using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Submissions;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Writes notification rows for the three events worth emailing about. Every method
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
}
