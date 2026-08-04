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

internal sealed class NotificationsByStatusSpecification : Specification<Notification>
{
    public NotificationsByStatusSpecification(NotificationStatus status)
    {
        Criteria = n => n.Status == status;
    }
}

internal sealed class NotificationsPagedSpecification : Specification<Notification>
{
    public NotificationsPagedSpecification(
        NotificationStatus? status,
        NotificationType? type,
        Guid? recipientId,
        string? search,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(n => n.Recipient);
        // Newest first: an outbox is read to find out what just happened.
        ApplyOrderByDescending(n => n.CreatedAtUtc);
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
