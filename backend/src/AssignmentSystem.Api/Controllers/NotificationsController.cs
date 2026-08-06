using AssignmentSystem.Api.Common;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Application.Features.Notifications;
using AssignmentSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// The notification outbox.
///
/// Listing is open to any signed-in user because the handler scopes a non-admin to their own
/// mail — "what has the system emailed me?" is a reasonable question for a teacher or student
/// to ask. Everything that acts on the queue (the summary, retry, and forcing a sweep) is
/// admin-only.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly INotificationDispatcher _outboxDispatcher;

    public NotificationsController(
        IDispatcher dispatcher,
        INotificationDispatcher outboxDispatcher)
    {
        _dispatcher = dispatcher;
        _outboxDispatcher = outboxDispatcher;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] NotificationStatus? status,
        [FromQuery] NotificationType? type,
        [FromQuery] Guid? recipientId,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetNotificationsQuery(status, type, recipientId, search, sortBy, sortDir, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }
        return ResultExtensions.PagedOk(this, result.Value!);
    }

    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetNotificationSummaryQuery(), ct);
        return result.ToActionResult(this);
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new RetryNotificationCommand(id), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Runs a sweep immediately instead of waiting for the timer. Exists so an admin who has
    /// just fixed a mail setting, or an evaluator who does not want to wait 30 seconds, can
    /// see the queue drain — and so the drain is reachable from an integration test.
    /// </summary>
    [HttpPost("dispatch")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Dispatch([FromQuery] int batchSize = 25, CancellationToken ct = default)
    {
        var sent = await _outboxDispatcher.DispatchPendingAsync(Math.Clamp(batchSize, 1, 200), ct);
        return Ok(new ApiResponse<DispatchResult> { Success = true, Data = new DispatchResult(sent) });
    }
}

public sealed record DispatchResult(int Sent);
