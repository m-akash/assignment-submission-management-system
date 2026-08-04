namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Hands a message to the mail server. The only thing in the notification path that
/// touches the network, which is exactly why it is a port: the outbox dispatcher is
/// testable without SMTP, and swapping SMTP for a transactional-email API later is one
/// class.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends one message. Throws on failure — the dispatcher records the error against
    /// the notification row and retries. Returning a bool would lose the reason.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Whether a real mail server is configured. False means the app is running without
    /// SMTP credentials (the default for a fresh local checkout), so notifications are
    /// logged instead of sent — the dispatcher says so once at startup rather than
    /// failing every row.
    /// </summary>
    bool IsConfigured { get; }
}

/// <summary>A single outbound email. Plain text body — see <c>SmtpEmailSender</c>.</summary>
public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string Body);
