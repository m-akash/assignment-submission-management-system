using System.Data.Common;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// Drains the outbox: claims a batch of rows, hands each to <see cref="IEmailSender"/>, and
/// records the outcome.
///
/// The claim is the important part. Selecting pending rows and sending them works only while
/// exactly one dispatcher is running — two instances would read the same rows and mail every
/// notification twice. Instead a single statement moves a batch to <c>Processing</c> under
/// <c>FOR UPDATE SKIP LOCKED</c>, so concurrent sweeps step over each other's rows and each
/// takes a disjoint batch. That is what makes the API horizontally scalable rather than
/// something that must be pinned to one instance.
///
/// Beyond that it deliberately does not:
///  - stop on a failure. One bad address must not block every message behind it, so each row
///    is attempted independently and its own error stored against it.
///  - save per row. One SaveChanges at the end keeps the write cost proportional to the sweep
///    rather than to the number of messages.
///
/// A crash after the claim commits but before the outcome is saved leaves rows in
/// <c>Processing</c>. They are not lost: the claim query also picks up rows claimed longer ago
/// than <c>ClaimTimeoutSeconds</c>, so the next sweep — on any instance — takes them back.
/// Retrying can therefore duplicate an email that was accepted just before a crash. That is
/// the right trade here: a student receiving one notice twice is a nuisance, never receiving
/// it is a missed deadline.
/// </summary>
internal sealed class NotificationDispatcher : INotificationDispatcher
{
    /// <summary>
    /// Claims a batch in one statement.
    ///
    /// <c>FOR UPDATE SKIP LOCKED</c> on the inner select is what makes this safe under
    /// concurrency: rows another transaction has locked are passed over rather than waited
    /// on, so two dispatchers sweeping at the same moment take different rows instead of one
    /// blocking behind the other. The subquery carries the ORDER BY and LIMIT because the
    /// locking clause cannot appear in an UPDATE directly.
    ///
    /// The second arm of the WHERE is the recovery path for rows stranded in Processing by a
    /// dispatcher that died — folding it in here means it costs nothing extra and needs no
    /// separate reaper.
    /// </summary>
    private const string ClaimSql = """
        UPDATE notifications
        SET status = @processing, claimed_at_utc = @now
        WHERE id IN (
            SELECT id FROM notifications
            WHERE (
                    status = @pending
                    AND attempt_count < @maxAttempts
                    AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= @now)
                  )
               OR (status = @processing AND claimed_at_utc < @staleBefore)
            ORDER BY created_at_utc
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
        )
        RETURNING id;
        """;

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
        var now = _clock.UtcNow;
        var claimedIds = await ClaimBatchAsync(batchSize, now, ct);
        if (claimedIds.Count == 0)
        {
            return 0;
        }

        // Read back through EF so the rows are tracked and the recipient name is available.
        var claimed = await _context.Notifications
            .Include(n => n.Recipient)
            .Where(n => claimedIds.Contains(n.Id))
            .OrderBy(n => n.CreatedAtUtc)
            .ToListAsync(ct);

        var retryBaseDelay = TimeSpan.FromSeconds(Math.Max(1, _options.RetryBackoffSeconds));
        var sent = 0;

        foreach (var notification in claimed)
        {
            // Stop taking on new work when shutting down, but still save what has been done
            // below — otherwise a successful send would be re-sent on the next start. Rows not
            // reached stay claimed and are reclaimed after the claim timeout.
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
                        // Body is composed and persisted as HTML (see NotificationMessages). The
                        // plain-text part is derived here at the dispatch boundary — the one place
                        // both halves of an EmailMessage are needed — so the row stores one body.
                        HtmlBody: notification.Body,
                        TextBody: HtmlToText.Convert(notification.Body)),
                    ct);

                notification.MarkSent(_clock.UtcNow);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Broad by design: any failure to reach a mail server is a failure to deliver,
                // and the dispatcher must survive all of them to reach the next row. The
                // specific reason is stored on the notification for the admin outbox to show.
                notification.MarkAttemptFailed(
                    _clock.UtcNow, ex.Message, _options.MaxDeliveryAttempts, retryBaseDelay);

                _logger.LogWarning(
                    ex,
                    "Notification {NotificationId} to {Email} failed on attempt {Attempt} of {MaxAttempts}; next attempt {NextAttempt}.",
                    notification.Id,
                    notification.RecipientEmail,
                    notification.AttemptCount,
                    _options.MaxDeliveryAttempts,
                    notification.NextAttemptAtUtc?.ToString("O") ?? "none — given up");
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification sweep: {Sent} sent, {Failed} failed, out of {Total} claimed.",
            sent, claimed.Count - sent, claimed.Count);

        return sent;
    }

    /// <summary>
    /// Runs <see cref="ClaimSql"/> and returns the ids it took. Raw ADO rather than an EF
    /// query: this is an UPDATE ... RETURNING, which EF's SQL passthrough would try to wrap
    /// in a subquery, and the locking clause has no LINQ equivalent at all.
    /// </summary>
    private async Task<List<Guid>> ClaimBatchAsync(int batchSize, DateTime now, CancellationToken ct)
    {
        var staleBefore = now.AddSeconds(-Math.Max(1, _options.ClaimTimeoutSeconds));

        var connection = _context.Database.GetDbConnection();
        await _context.Database.OpenConnectionAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ClaimSql;
            // Enlists in the ambient transaction when there is one, so a caller that wraps a
            // sweep (an integration test, say) sees consistent state.
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            AddParameter(command, "processing", (int)NotificationStatus.Processing);
            AddParameter(command, "pending", (int)NotificationStatus.Pending);
            AddParameter(command, "now", now);
            AddParameter(command, "staleBefore", staleBefore);
            AddParameter(command, "maxAttempts", _options.MaxDeliveryAttempts);
            AddParameter(command, "batchSize", Math.Max(1, batchSize));

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
