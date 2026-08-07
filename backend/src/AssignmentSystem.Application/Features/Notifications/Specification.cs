using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;

namespace AssignmentSystem.Application.Features.Notifications;

internal sealed class NotificationWithRecipientSpecification : Specification<Notification>
{
    public NotificationWithRecipientSpecification(Guid id)
    {
        Criteria = n => n.Id == id;
        AddInclude(n => n.Recipient);
    }
}

/// <summary>
/// Loads a set of rows by id for the bulk delete. The handler validates the list is
/// non-empty first; <c>Contains</c> on the list translates to SQL <c>IN (...)</c>. Rows
/// already soft-deleted are excluded by the global query filter, so only live rows come back.
/// </summary>
internal sealed class NotificationsByIdsSpecification : Specification<Notification>
{
    public NotificationsByIdsSpecification(IReadOnlyList<Guid> ids)
    {
        Criteria = n => ids.Contains(n.Id);
    }
}

internal sealed class NotificationsByStatusSpecification : Specification<Notification>
{
    public NotificationsByStatusSpecification(NotificationStatus status)
    {
        Criteria = n => n.Status == status;
    }
}

/// <summary>
/// Everything still owed a delivery — queued, plus whatever a dispatcher currently holds.
/// </summary>
internal sealed class NotificationsAwaitingDeliverySpecification : Specification<Notification>
{
    public NotificationsAwaitingDeliverySpecification()
    {
        Criteria = n => n.Status == NotificationStatus.Pending || n.Status == NotificationStatus.Processing;
    }
}

internal sealed class NotificationsPagedSpecification : Specification<Notification>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Notification> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Notification, object>>>
        {
            ["recipient"] = n => n.RecipientEmail,
            ["subject"] = n => n.Subject,
            ["status"] = n => n.Status,
            ["type"] = n => n.Type,
            ["sentAt"] = n => n.SentAtUtc!,
            ["createdAt"] = n => n.CreatedAtUtc,
        },
        tieBreaker: n => n.Id);

    public NotificationsPagedSpecification(
        NotificationStatus? status,
        NotificationType? type,
        Guid? recipientId,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(n => n.Recipient);
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            // Newest first: an outbox is read to find out what just happened.
            ApplyOrderByDescending(n => n.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = n =>
            (!status.HasValue || n.Status == status.Value) &&
            (!type.HasValue || n.Type == type.Value) &&
            (!recipientId.HasValue || n.RecipientId == recipientId.Value) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             n.RecipientEmail.Contains(searchLower) ||
             n.Subject.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
