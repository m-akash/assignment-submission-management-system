namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Drains the notification outbox: takes a batch of pending rows, tries to send each, and
/// records the outcome. Driven by a hosted service in the API on a timer, and callable
/// directly from a test so the drain can be asserted without waiting on one.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Processes up to <paramref name="batchSize"/> pending notifications, oldest first.
    /// Returns how many were sent successfully. Never throws for a delivery failure — a
    /// failed send is recorded on the row, because the dispatcher's job is to keep running.
    /// </summary>
    Task<int> DispatchPendingAsync(int batchSize, CancellationToken ct = default);
}
