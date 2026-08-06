using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Notifications;
using AssignmentSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The outbox claim under concurrency.
///
/// Before the claim existed, two dispatchers selecting Pending rows would both read the same
/// batch and mail every notification twice — which meant the API could only ever be run as a
/// single instance. These tests are the evidence that is no longer true, so they drive the
/// dispatcher directly and in parallel rather than through the endpoint.
/// </summary>
public sealed class OutboxConcurrencyTests : IntegrationTestBase
{
    public OutboxConcurrencyTests(ApiFactory api) : base(api) { }

    /// <summary>
    /// Clears whatever earlier tests left queued. The suite shares one database, so a sweep
    /// started here would otherwise also pick up their rows — and "how many did this sweep
    /// send" is only a meaningful number when every row in flight is one this test queued.
    /// Safe to do serially: the whole integration suite is one xUnit collection.
    /// </summary>
    private async Task DrainExistingAsync()
    {
        while (await SweepAsync(200) > 0)
        {
        }
    }

    /// <summary>Queues rows addressed to a tag unique to the calling test.</summary>
    private async Task<string> QueueAsync(int count)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];

        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipientId = await db.Users.Select(u => u.Id).FirstAsync();

        for (var i = 0; i < count; i++)
        {
            db.Notifications.Add(Notification.Queue(
                recipientId,
                $"{tag}-{i}@outbox.local",
                NotificationType.AssignmentPublished,
                $"Subject {tag} {i}",
                "<p>body</p>"));
        }

        await db.SaveChangesAsync();
        return tag;
    }

    private async Task<List<Notification>> RowsAsync(string tag)
    {
        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications
            .Where(n => n.RecipientEmail.StartsWith(tag))
            .ToListAsync();
    }

    /// <summary>
    /// Each dispatcher gets its own DI scope, so each has its own DbContext and its own
    /// database connection — which is what makes SKIP LOCKED meaningful.
    /// </summary>
    private async Task<int> SweepAsync(int batchSize)
    {
        await using var scope = Api.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await dispatcher.DispatchPendingAsync(batchSize, CancellationToken.None);
    }

    /// <summary>
    /// The headline guarantee: run several sweeps at once and every row is delivered exactly
    /// once between them. Without the claim, each sweep would send all of them.
    /// </summary>
    [Fact]
    public async Task ConcurrentSweeps_DeliverEachRowExactlyOnce()
    {
        const int RowCount = 24;
        const int Dispatchers = 4;

        await DrainExistingAsync();
        var tag = await QueueAsync(RowCount);

        var sentCounts = await Task.WhenAll(
            Enumerable.Range(0, Dispatchers).Select(_ => SweepAsync(RowCount)));

        // No mail server is configured under test, so the sender logs and reports success —
        // which is exactly what makes "how many were sent" a usable count here.
        sentCounts.Sum().Should().Be(RowCount, "every row is delivered, and none of them twice");

        var rows = await RowsAsync(tag);
        rows.Should().HaveCount(RowCount);
        rows.Should().OnlyContain(n => n.Status == NotificationStatus.Sent);
        rows.Should().OnlyContain(n => n.AttemptCount == 1, "a claimed row is attempted by one dispatcher only");
    }

    [Fact]
    public async Task ASweep_TakesNoMoreThanItsBatchSize()
    {
        await DrainExistingAsync();
        var tag = await QueueAsync(10);

        var sent = await SweepAsync(4);

        sent.Should().Be(4);
        (await RowsAsync(tag))
            .Count(n => n.Status == NotificationStatus.Pending)
            .Should().Be(6);
    }

    /// <summary>
    /// A dispatcher that dies mid-batch leaves rows claimed. They must not be stranded — the
    /// next sweep takes them back once the claim has gone stale. Simulated by claiming rows
    /// and back-dating the claim, since killing a process mid-test is not practical.
    /// </summary>
    [Fact]
    public async Task RowsStrandedByADeadDispatcher_AreReclaimedOnceTheClaimGoesStale()
    {
        await DrainExistingAsync();
        var tag = await QueueAsync(3);

        await using (var scope = Api.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stranded = await db.Notifications.Where(n => n.RecipientEmail.StartsWith(tag)).ToListAsync();
            foreach (var row in stranded)
            {
                // Claimed an hour ago by a process that never came back.
                row.MarkClaimed(DateTime.UtcNow.AddHours(-1));
            }
            await db.SaveChangesAsync();
        }

        var sent = await SweepAsync(10);

        sent.Should().Be(3);
        (await RowsAsync(tag)).Should().OnlyContain(n => n.Status == NotificationStatus.Sent);
    }

    /// <summary>
    /// A freshly claimed row is another dispatcher's work in progress, not something to take.
    /// This is the same mechanism as above, seen from the other side — without it the stale
    /// reclaim would just be a race with extra steps.
    /// </summary>
    [Fact]
    public async Task AFreshlyClaimedRow_IsNotPickedUpByAnotherSweep()
    {
        await DrainExistingAsync();
        var tag = await QueueAsync(2);

        await using (var scope = Api.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var row in await db.Notifications.Where(n => n.RecipientEmail.StartsWith(tag)).ToListAsync())
            {
                row.MarkClaimed(DateTime.UtcNow);
            }
            await db.SaveChangesAsync();
        }

        var sent = await SweepAsync(10);

        sent.Should().Be(0);
        (await RowsAsync(tag)).Should().OnlyContain(n => n.Status == NotificationStatus.Processing);
    }
}
