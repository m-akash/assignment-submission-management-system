using System;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Application.Tests.DomainTests;

/// <summary>
/// The outbox row's retry state machine. Exercised here rather than through the dispatcher
/// because the interesting cases — the last attempt, the backoff curve — would otherwise
/// need a test that waits minutes to observe them.
/// </summary>
public class NotificationTests
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
    private static readonly DateTime Now = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    private static Notification Queued() => Notification.Queue(
        Guid.NewGuid(), "someone@test.local", NotificationType.AssignmentPublished, "Subject", "<p>Body</p>");

    [Fact]
    public void AQueuedNotification_IsImmediatelyDeliverable()
    {
        Queued().IsDeliverable(MaxAttempts, Now).Should().BeTrue();
    }

    [Fact]
    public void AFailedAttempt_ReturnsTheRowToPendingBehindABackoff()
    {
        var notification = Queued();

        notification.MarkAttemptFailed(Now, "connection refused", MaxAttempts, BaseDelay);

        notification.Status.Should().Be(NotificationStatus.Pending, "retries remain");
        notification.NextAttemptAtUtc.Should().Be(Now.Add(BaseDelay));
        notification.LastError.Should().Be("connection refused");
    }

    /// <summary>
    /// The behaviour the backoff exists for: a row that just failed is not eligible again on
    /// the very next sweep. Without this the retry budget is spent in ninety seconds against
    /// a server that needed a minute to come back.
    /// </summary>
    [Fact]
    public void ARowInsideItsBackoffWindow_IsNotDeliverableYet()
    {
        var notification = Queued();
        notification.MarkAttemptFailed(Now, "timeout", MaxAttempts, BaseDelay);

        notification.IsDeliverable(MaxAttempts, Now.AddSeconds(10)).Should().BeFalse();
        notification.IsDeliverable(MaxAttempts, Now.Add(BaseDelay)).Should().BeTrue();
    }

    [Fact]
    public void TheBackoff_DoublesWithEachFailure()
    {
        Notification.BackoffFor(1, BaseDelay).Should().Be(BaseDelay);
        Notification.BackoffFor(2, BaseDelay).Should().Be(BaseDelay * 2);
        Notification.BackoffFor(3, BaseDelay).Should().Be(BaseDelay * 4);
    }

    /// <summary>Capped rather than overflowing into a nonsensical or negative delay.</summary>
    [Fact]
    public void TheBackoff_IsBoundedForAnAbsurdAttemptCount()
    {
        Notification.BackoffFor(1000, BaseDelay)
            .Should().Be(BaseDelay * 1024, "the exponent is clamped at 2^10");
    }

    [Fact]
    public void UsingUpEveryAttempt_ParksTheRowAsFailedWithNoFurtherSchedule()
    {
        var notification = Queued();

        for (var i = 0; i < MaxAttempts; i++)
        {
            notification.MarkAttemptFailed(Now, "nope", MaxAttempts, BaseDelay);
        }

        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.NextAttemptAtUtc.Should().BeNull("there is nothing left to schedule");
        notification.IsDeliverable(MaxAttempts, Now.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void AnAdminRetry_ClearsTheAttemptCountAndTheBackoff()
    {
        var notification = Queued();
        for (var i = 0; i < MaxAttempts; i++)
        {
            notification.MarkAttemptFailed(Now, "nope", MaxAttempts, BaseDelay);
        }

        notification.RequeueForRetry();

        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.AttemptCount.Should().Be(0);
        notification.NextAttemptAtUtc.Should().BeNull();
        notification.IsDeliverable(MaxAttempts, Now).Should().BeTrue();
    }

    [Fact]
    public void AClaimedRow_IsHiddenFromOtherDispatchers()
    {
        var notification = Queued();

        notification.MarkClaimed(Now);

        notification.Status.Should().Be(NotificationStatus.Processing);
        notification.ClaimedAtUtc.Should().Be(Now);
        notification.IsDeliverable(MaxAttempts, Now).Should().BeFalse();
    }

    [Fact]
    public void ASuccessfulSend_ReleasesTheClaimAndClearsTheSchedule()
    {
        var notification = Queued();
        notification.MarkAttemptFailed(Now, "first try", MaxAttempts, BaseDelay);
        notification.MarkClaimed(Now.AddMinutes(1));

        notification.MarkSent(Now.AddMinutes(2));

        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ClaimedAtUtc.Should().BeNull();
        notification.NextAttemptAtUtc.Should().BeNull();
        notification.LastError.Should().NotBeNull("the record that delivery was shaky is kept");
    }

    [Fact]
    public void AlreadySentNotifications_CannotBeRequeued()
    {
        var notification = Queued();
        notification.MarkSent(Now);

        var act = notification.RequeueForRetry;

        act.Should().Throw<AssignmentSystem.Domain.Common.DomainException>();
    }
}
