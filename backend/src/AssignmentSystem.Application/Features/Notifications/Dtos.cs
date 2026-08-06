using AssignmentSystem.Application.Common.Authorization;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// An outbox row as the admin screen shows it. The body is included so an admin can see
/// exactly what was (or would be) sent — the whole value of an outbox is that it is
/// inspectable rather than something you infer from mail-server logs.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    Guid RecipientId,
    string RecipientName,
    string RecipientEmail,
    NotificationType Type,
    string Subject,
    string Body,
    NotificationStatus Status,
    int AttemptCount,
    DateTime? LastAttemptAtUtc,
    DateTime? SentAtUtc,
    string? LastError,
    Guid? AssignmentId,
    Guid? SubmissionId,
    DateTime CreatedAtUtc
);

/// <summary>Counts per delivery state, for the outbox header.</summary>
public sealed record NotificationSummaryDto(
    int Pending,
    int Sent,
    int Failed
);

[RequiresAuthentication]
public sealed record GetNotificationsQuery(
    NotificationStatus? Status = null,
    NotificationType? Type = null,
    Guid? RecipientId = null,
    string? Search = null,
    /// <summary>Sort key from the endpoint's allow-list; anything else falls back to its natural order.</summary>
    string? SortBy = null,
    /// <summary>"desc" for descending; ascending otherwise.</summary>
    string? SortDir = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<PageResult<NotificationDto>>;

[RequiresRole(Role.Admin)]
public sealed record GetNotificationSummaryQuery : IQuery<NotificationSummaryDto>;

/// <summary>Puts a failed row back in the queue once the mail problem is fixed.</summary>
[RequiresRole(Role.Admin)]
public sealed record RetryNotificationCommand(Guid Id) : ICommand<NotificationDto>;
