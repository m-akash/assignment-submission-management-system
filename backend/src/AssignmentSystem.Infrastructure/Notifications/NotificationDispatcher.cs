using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// Drains the outbox. Reads pending rows oldest-first, hands each to
/// <see cref="IEmailSender"/>, and records the outcome on the row.
///
/// Two things it deliberately does not do:
///  - It does not stop on a failure. One bad address must not block every message behind it,
///    so each row is attempted independently and its own error stored against it.
///  - It does not save per row. One SaveChanges at the end of the batch keeps the write cost
///    proportional to the sweep rather than to the number of messages; a crash mid-batch
///    leaves those rows Pending, which is the safe direction — the next sweep retries them.
///
/// Retrying can therefore duplicate an email that was accepted just before a crash. That is
/// the right trade for this: a student receiving one notice twice is a nuisance, never
/// receiving it is a missed deadline.
/// </summary>
internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly EmailOptions _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        AppDbContext context,
        IEmailSender emailSender,
        IClock clock,
        IOptions<EmailOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var maxAttempts = _options.MaxDeliveryAttempts;

        // Oldest first, and only rows with attempts left. AttemptCount is filtered here as
        // well as in the status transition so a row can never be picked up forever if
        // something else has been incrementing it.
        var pending = await _context.Notifications
            .Include(n => n.Recipient)
            .Where(n => n.Status == NotificationStatus.Pending && n.AttemptCount < maxAttempts)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sent = 0;

        foreach (var notification in pending)
        {
            // Stop taking on new work when shutting down, but still save what has been done
            // below — otherwise a successful send would be re-sent on the next start.
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Recipient can come back null despite the Include: users are soft-deleted,
                // and the query filter on them hides the row from the join. The address is
                // snapshotted on the notification precisely so this still delivers — only
                // the display name is lost.
                var recipientName = notification.Recipient is { } recipient ? recipient.FullName : string.Empty;

                await _emailSender.SendAsync(
                    new EmailMessage(
                        notification.RecipientEmail,
                        recipientName,
                        notification.Subject,
                        notification.Body),
                    ct);

                notification.MarkSent(_clock.UtcNow);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Broad by design: any failure to reach a mail server is a failure to deliver,
                // and the dispatcher must survive all of them to reach the next row. The
                // specific reason is stored on the notification for the admin outbox to show.
                notification.MarkAttemptFailed(_clock.UtcNow, ex.Message, maxAttempts);

                _logger.LogWarning(
                    ex,
                    "Notification {NotificationId} to {Email} failed on attempt {Attempt} of {MaxAttempts}.",
                    notification.Id, notification.RecipientEmail, notification.AttemptCount, maxAttempts);
            }
        }

        await _context.SaveChangesAsync(ct);

        if (sent > 0 || pending.Count > 0)
        {
            _logger.LogInformation(
                "Notification sweep: {Sent} sent, {Failed} failed, out of {Total} pending.",
                sent, pending.Count - sent, pending.Count);
        }

        return sent;
    }
}
