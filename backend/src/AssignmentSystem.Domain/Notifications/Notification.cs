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

    /// <summary>
    /// Earliest time this row may be attempted again, set by the backoff after a failure.
    /// Null means "eligible now". Without it a failing row is retried at the full sweep
    /// cadence, which turns one unreachable mail server into a tight loop against it.
    /// </summary>
    public DateTime? NextAttemptAtUtc { get; private set; }

    /// <summary>
    /// When a dispatcher claimed this row. Doubles as the liveness signal: a row still
    /// claimed after the claim timeout belonged to a process that died, and the next sweep
    /// takes it back. Cleared whenever the attempt resolves.
    /// </summary>
    public DateTime? ClaimedAtUtc { get; private set; }

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
        ClaimedAtUtc = null;
        NextAttemptAtUtc = null;
    }

    /// <summary>
    /// An attempt failed. Returns to <c>Pending</c> behind a backoff while retries remain so
    /// the dispatcher picks it up again later, and only becomes <c>Failed</c> once
    /// <paramref name="maxAttempts"/> is used up — the terminal state means "given up", not
    /// "one attempt missed".
    /// </summary>
    public void MarkAttemptFailed(DateTime attemptedAtUtc, string error, int maxAttempts, TimeSpan retryBaseDelay)
    {
        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        LastError = Truncate(error, 2000);
        ClaimedAtUtc = null;

        if (AttemptCount >= maxAttempts)
        {
            Status = NotificationStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }

        Status = NotificationStatus.Pending;
        NextAttemptAtUtc = attemptedAtUtc.Add(BackoffFor(AttemptCount, retryBaseDelay));
    }

    /// <summary>
    /// Exponential: the delay doubles with each failure. A mail server that is briefly busy
    /// is retried almost immediately, while one that is genuinely down is backed away from
    /// instead of being hit once per sweep until the attempt budget runs out.
    /// </summary>
    public static TimeSpan BackoffFor(int attemptCount, TimeSpan baseDelay)
    {
        // Capped before shifting: 2^31 ticks of delay is not a meaningful schedule, and the
        // multiplication would overflow long before it got there.
        var exponent = Math.Min(attemptCount - 1, 10);
        return baseDelay * Math.Pow(2, exponent);
    }

    /// <summary>
    /// Taken by a dispatcher for delivery. The state change is what hides the row from every
    /// other dispatcher; committing it before the send is what makes running more than one
    /// instance safe.
    /// </summary>
    public void MarkClaimed(DateTime claimedAtUtc)
    {
        Status = NotificationStatus.Processing;
        ClaimedAtUtc = claimedAtUtc;
    }

    /// <summary>
    /// Puts a <c>Failed</c> row back in the queue with its attempt count and backoff reset,
    /// for the admin "retry" action once the underlying mail problem is fixed.
    /// </summary>
    public void RequeueForRetry()
    {
        if (Status == NotificationStatus.Sent)
        {
            throw new DomainException("This notification has already been sent.");
        }

        Status = NotificationStatus.Pending;
        AttemptCount = 0;
        NextAttemptAtUtc = null;
        ClaimedAtUtc = null;
    }

    /// <summary>Whether the dispatcher should try this row now (retry budget and backoff).</summary>
    public bool IsDeliverable(int maxAttempts, DateTime utcNow) =>
        Status == NotificationStatus.Pending
        && AttemptCount < maxAttempts
        && (NextAttemptAtUtc is null || NextAttemptAtUtc <= utcNow);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
