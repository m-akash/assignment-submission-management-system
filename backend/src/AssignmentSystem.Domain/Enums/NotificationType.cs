namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// What a notification is about. Drives the subject/body the composer builds and lets
/// the admin outbox view be filtered by event rather than by reading subject lines.
/// </summary>
public enum NotificationType
{
    /// <summary>A teacher published an assignment — sent to every student enrolled in its class.</summary>
    AssignmentPublished = 0,

    /// <summary>A student submitted — sent to the teacher who owns the assignment.</summary>
    SubmissionReceived = 1,

    /// <summary>A teacher graded a submission — sent to the student who owns it.</summary>
    SubmissionGraded = 2,
}
