using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Domain.Notifications;

/// <summary>
/// A queued notification email — a transactional outbox row, not a log of one already
/// sent. The row is written in the same transaction as the change that caused it
/// (publish, submit, grade); a background dispatcher picks up <c>Pending</c> rows and
/// hands them to the mail server afterwards.
///
/// The indirection is what makes the feature survive reality: an SMTP server that is
/// slow, down, or misconfigured cannot fail the request that triggered it, nothing is
/// silently lost when it does, retries are bounded and recorded, and a test can assert
/// on rows instead of intercepting mail.
///
/// <see cref="RecipientEmail"/> is snapshotted deliberately: it is the address the mail
/// was queued for, so the outbox still explains where a message went after the user has
/// changed their address or been deleted.
/// </summary>
public sealed class Notification : BaseEntity
{
    public Guid RecipientId { get; private set; }
    public ApplicationUser Recipient { get; private set; } = null!;

    /// <summary>The address as it stood when the mail was queued (see class remarks).</summary>
    public string RecipientEmail { get; private set; } = null!;

    public NotificationType Type { get; private set; }

    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;

    /// <summary>How many delivery attempts have been made — bounds the retry loop.</summary>
    public int AttemptCount { get; private set; }

    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>Why the most recent attempt failed. Kept after a later success too — it
    /// is the only record that delivery was ever shaky.</summary>
    public string? LastError { get; private set; }

    // ── Context (nullable: not every type relates to both) ────────────────────
    // Plain ids with no navigation or FK: the outbox has to outlive what it refers to.
    // A notification about a deleted assignment is still a true record of a mail sent,
    // and an FK would either block the delete or take the history with it.
    public Guid? AssignmentId { get; private set; }
    public Guid? SubmissionId { get; private set; }

    private Notification() { }

    public static Notification Queue(
        Guid recipientId,
        string recipientEmail,
        NotificationType type,
        string subject,
        string body,
        Guid? assignmentId = null,
        Guid? submissionId = null)
    {
        if (recipientId == Guid.Empty)
        {
            throw new DomainException("A notification needs a recipient.");
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new DomainException("A notification needs a recipient email address.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainException("A notification needs a subject.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainException("A notification needs a body.");
        }

        return new Notification
        {
            RecipientId = recipientId,
            RecipientEmail = recipientEmail.Trim().ToLowerInvariant(),
            Type = type,
            Subject = subject.Trim(),
            Body = body,
            Status = NotificationStatus.Pending,
            AssignmentId = assignmentId,
            SubmissionId = submissionId,
        };
    }

    /// <summary>The mail server accepted the message.</summary>
    public void MarkSent(DateTime sentAtUtc)
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = sentAtUtc;
        LastAttemptAtUtc = sentAtUtc;
        AttemptCount++;
    }

    /// <summary>
    /// An attempt failed. Stays <c>Pending</c> while retries remain so the dispatcher
    /// picks it up again, and only becomes <c>Failed</c> once <paramref name="maxAttempts"/>
    /// is used up — the terminal state means "given up", not "one attempt missed".
    /// </summary>
    public void MarkAttemptFailed(DateTime attemptedAtUtc, string error, int maxAttempts)
    {
        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        LastError = Truncate(error, 2000);
        Status = AttemptCount >= maxAttempts ? NotificationStatus.Failed : NotificationStatus.Pending;
    }

    /// <summary>
    /// Puts a <c>Failed</c> row back in the queue with its attempt count reset, for the
    /// admin "retry" action once the underlying mail problem is fixed.
    /// </summary>
    public void RequeueForRetry()
    {
        if (Status == NotificationStatus.Sent)
        {
            throw new DomainException("This notification has already been sent.");
        }

        Status = NotificationStatus.Pending;
        AttemptCount = 0;
    }

    /// <summary>Whether the dispatcher should try this row (used by the retry budget).</summary>
    public bool IsDeliverable(int maxAttempts) =>
        Status == NotificationStatus.Pending && AttemptCount < maxAttempts;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
