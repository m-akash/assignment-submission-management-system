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
        var recipientId = _currentUser.Role == Role.Admin
            ? query.RecipientId
            : _currentUser.UserId;

        var spec = new NotificationsPagedSpecification(
            query.Status, query.Type, recipientId, query.Search, query.Page, query.PageSize);
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
        var pending = await _notifications.CountAsync(new NotificationsByStatusSpecification(NotificationStatus.Pending), ct);
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
