namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Delivery state of a queued notification email.
/// Pending → written, not yet handed to the mail server.
/// Sent    → the mail server accepted it.
/// Failed  → every retry was used up; the last error is kept on the row.
/// </summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}
