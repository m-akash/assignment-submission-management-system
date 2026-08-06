namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Delivery state of a queued notification email.
/// Pending    → written, waiting to be picked up.
/// Processing → claimed by a dispatcher that is about to hand it to the mail server.
/// Sent       → the mail server accepted it.
/// Failed     → every retry was used up; the last error is kept on the row.
/// </summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,

    /// <summary>
    /// Claimed by one dispatcher and invisible to the others. Transient: a row leaves this
    /// state as soon as the attempt resolves, and a sweep reclaims any row left stranded
    /// here by a process that died mid-batch (see <c>ClaimedAtUtc</c>).
    ///
    /// Appended rather than inserted in order: the values are persisted as integers, so
    /// renumbering would silently reinterpret every existing row.
    /// </summary>
    Processing = 3,
}
