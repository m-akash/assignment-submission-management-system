using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// The outbox list. Admin-only at the controller, and scoped again here for anyone else
/// who reaches it: a teacher or student sees only mail addressed to them, never the
/// school's whole outbound queue.
/// </summary>
public sealed class GetNotificationsHandler : IQueryHandler<GetNotificationsQuery, PageResult<NotificationDto>>
{
    private readonly IRepository<Notification> _notifications;
    private readonly ICurrentUser _currentUser;
    private static readonly NotificationMapper Mapper = new();

    public GetNotificationsHandler(IRepository<Notification> notifications, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result<PageResult<NotificationDto>>> HandleAsync(GetNotificationsQuery query, CancellationToken ct = default)
    {
        // Anyone but an admin is pinned to their own row, whatever recipients they asked for.
        IReadOnlyList<Guid>? recipientIds = _currentUser.Role == Role.Admin
            ? query.RecipientIds
            : _currentUser.UserId is { } userId ? [userId] : null;

        var spec = new NotificationsPagedSpecification(
            query.Statuses, query.Types, recipientIds, query.Search, query.SortBy, query.SortDir, query.Page, query.PageSize);
        var paged = await _notifications.ListPagedAsync(spec, ct);

        var items = paged.Items.Select(Mapper.MapToDto).ToList();
        return new PageResult<NotificationDto>(items, paged.Page, paged.PageSize, paged.Total);
    }
}

public sealed class GetNotificationSummaryHandler : IQueryHandler<GetNotificationSummaryQuery, NotificationSummaryDto>
{
    private readonly IRepository<Notification> _notifications;

    public GetNotificationSummaryHandler(IRepository<Notification> notifications)
    {
        _notifications = notifications;
    }

    public async Task<Result<NotificationSummaryDto>> HandleAsync(GetNotificationSummaryQuery query, CancellationToken ct = default)
    {
        // Processing counts as pending: a row a dispatcher is holding is still queued as far
        // as an admin reading this screen is concerned, and leaving it out of every bucket
        // would make the three counts silently fail to add up to the outbox.
        var pending = await _notifications.CountAsync(new NotificationsAwaitingDeliverySpecification(), ct);
        var sent = await _notifications.CountAsync(new NotificationsByStatusSpecification(NotificationStatus.Sent), ct);
        var failed = await _notifications.CountAsync(new NotificationsByStatusSpecification(NotificationStatus.Failed), ct);

        return new NotificationSummaryDto(pending, sent, failed);
    }
}

/// <summary>
/// Re-queues a failed notification. The dispatcher only looks at Pending rows, so a row
/// that used up its attempts stays put until someone decides the underlying problem
/// (wrong SMTP host, a bounced address) is fixed.
/// </summary>
public sealed class RetryNotificationHandler : ICommandHandler<RetryNotificationCommand, NotificationDto>
{
    private readonly IRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly NotificationMapper Mapper = new();

    public RetryNotificationHandler(IRepository<Notification> notifications, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationDto>> HandleAsync(RetryNotificationCommand command, CancellationToken ct = default)
    {
        var spec = new NotificationWithRecipientSpecification(command.Id);
        var notification = await _notifications.FirstOrDefaultAsync(spec, ct);
        if (notification is null)
        {
            return Result<NotificationDto>.Failure(Error.NotFound(
                "Notification.NotFound", "The specified notification was not found."));
        }

        try
        {
            notification.RequeueForRetry();
            _notifications.Update(notification);
            await _unitOfWork.SaveChangesAsync(ct);

            return Mapper.MapToDto(notification);
        }
        catch (DomainException ex)
        {
            return Result<NotificationDto>.Failure(Error.Validation("Notification.Invalid", ex.Message));
        }
    }
}

/// <summary>
/// Hides one outbox row. Soft delete: the row stays in the table (an audit record) but the
/// global query filter drops it from reads and from the dispatcher's claim sweep alike, so a
/// deleted email is never sent.
/// </summary>
public sealed class DeleteNotificationHandler : ICommandHandler<DeleteNotificationCommand>
{
    private readonly IRepository<Notification> _notifications;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteNotificationHandler(IRepository<Notification> notifications, IClock clock, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(DeleteNotificationCommand command, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(command.Id, ct);
        if (notification is null)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", "The specified notification was not found."));
        }

        notification.SoftDelete(_clock.UtcNow);
        _notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

/// <summary>
/// Hides many rows in one transaction. Already-deleted rows are invisible to the query
/// (global filter) and so are simply not returned — the count reflects rows actually hidden
/// this call, which is what the UI toasts.
/// </summary>
public sealed class BulkDeleteNotificationsHandler : ICommandHandler<BulkDeleteNotificationsCommand, BulkDeleteResult>
{
    private const int MaxIdsPerBatch = 500;

    private readonly IRepository<Notification> _notifications;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public BulkDeleteNotificationsHandler(IRepository<Notification> notifications, IClock clock, IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BulkDeleteResult>> HandleAsync(BulkDeleteNotificationsCommand command, CancellationToken ct = default)
    {
        if (command.Ids.Count == 0)
        {
            return Result<BulkDeleteResult>.Failure(Error.Validation("Notification.NoIds", "At least one notification id is required."));
        }

        if (command.Ids.Count > MaxIdsPerBatch)
        {
            return Result<BulkDeleteResult>.Failure(Error.Validation("Notification.TooMany", $"A maximum of {MaxIdsPerBatch} notifications can be deleted at once."));
        }

        var live = await _notifications.ListAsync(new NotificationsByIdsSpecification(command.Ids), ct);

        foreach (var notification in live)
        {
            notification.SoftDelete(_clock.UtcNow);
            _notifications.Update(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new BulkDeleteResult(live.Count);
    }
}
