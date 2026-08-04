namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// The handful of configured values the notification bodies need. A port rather than an
/// <c>IOptions&lt;T&gt;</c> dependency so the Application layer stays free of configuration
/// plumbing, and so a test can compose a message without a configuration provider.
/// </summary>
public interface INotificationSettings
{
    /// <summary>
    /// Where the frontend is reachable, used to build the "open it here" link in an email
    /// body. Empty when unset — the composer then omits the link rather than emitting a
    /// broken one.
    /// </summary>
    string AppBaseUrl { get; }

    /// <summary>
    /// How many delivery attempts a notification gets before it is marked Failed.
    /// Read by the dispatcher and passed into <c>Notification.MarkAttemptFailed</c>.
    /// </summary>
    int MaxDeliveryAttempts { get; }
}
