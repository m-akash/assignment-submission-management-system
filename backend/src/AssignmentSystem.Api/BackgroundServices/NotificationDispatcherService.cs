using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Api.BackgroundServices;

/// <summary>
/// Sweeps the notification outbox on a timer.
///
/// A thin shell on purpose: the draining logic is <see cref="INotificationDispatcher"/> in
/// Infrastructure, and this class only owns the things that are genuinely host concerns —
/// the interval, the per-sweep DI scope, and surviving until shutdown. That split is also
/// what lets a test drain the outbox synchronously instead of waiting on a timer.
///
/// Nothing here throws. An exception escaping <c>ExecuteAsync</c> takes the whole host down
/// on .NET's default <c>BackgroundServiceExceptionBehavior</c>, and the API must not stop
/// serving requests because a mail server is unreachable.
/// </summary>
internal sealed class NotificationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailOptions> options,
        ILogger<NotificationDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Clamped: a misconfigured 0 would spin the loop against the database.
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.DispatchIntervalSeconds));

        if (_options.IsConfigured)
        {
            _logger.LogInformation(
                "Notification dispatcher started — sweeping every {Interval}s via {Host}:{Port}.",
                interval.TotalSeconds, _options.Host, _options.Port);
        }
        else
        {
            // Said once, plainly, at startup. Otherwise "no emails arrived" looks like a bug
            // rather than an unset environment variable.
            _logger.LogWarning(
                "Notification dispatcher started, but no SMTP host is configured (Email:Host). " +
                "Notifications will be queued and logged instead of emailed — see .env.example.");
        }

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A fresh scope per sweep: the dispatcher depends on a scoped DbContext, and
                // one held for the lifetime of the host would accumulate tracked entities.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

                await dispatcher.DispatchPendingAsync(_options.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log and keep the loop alive — the next sweep retries whatever was left Pending.
                _logger.LogError(ex, "Notification sweep failed; retrying in {Interval}s.", interval.TotalSeconds);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Notification dispatcher stopped.");
    }
}
