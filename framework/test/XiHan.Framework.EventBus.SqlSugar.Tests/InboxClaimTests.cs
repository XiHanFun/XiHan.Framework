// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱领取测试
/// </summary>
public class InboxClaimTests
{
    /// <summary>
    /// 领取后记录被标记为已领取并带令牌
    /// </summary>
    [Fact]
    public async Task 领取后记录被标记并带令牌()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);

        var claimed = await context.Inbox.GetWaitingEventsAsync(10);

        Assert.Single(claimed);
        Assert.Equal(info.Id, claimed[0].Id);
        Assert.Equal(info.MessageId, claimed[0].MessageId);

        var stored = await context.Client.Queryable<SysEventInbox>()
            .Where(item => item.BasicId == info.Id)
            .FirstAsync();

        Assert.Equal(SysEventInbox.StatusClaimed, stored.Status);
        Assert.False(string.IsNullOrWhiteSpace(stored.ClaimToken));
        Assert.NotNull(stored.ClaimTime);
    }

    /// <summary>
    /// 已领取的记录不会被再次领取
    /// </summary>
    [Fact]
    public async Task 已领取的记录不会被再次领取()
    {
        using var context = new InboxTestContext();
        await context.Inbox.EnqueueAsync(NewEvent());

        var first = await context.Inbox.GetWaitingEventsAsync(10);
        var second = await context.Inbox.GetWaitingEventsAsync(10);

        Assert.Single(first);
        Assert.Empty(second);
    }

    /// <summary>
    /// 领取超时后记录可被重新领取
    /// </summary>
    [Fact]
    public async Task 领取超时后可被重新领取()
    {
        using var context = new InboxTestContext(claimTimeout: TimeSpan.FromMinutes(5));
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        var stale = DateTimeOffset.UtcNow.AddDays(-1);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { ClaimTime = stale })
            .Where(item => item.BasicId == info.Id)
            .ExecuteCommandAsync();

        var reclaimed = await context.Inbox.GetWaitingEventsAsync(10);

        Assert.Single(reclaimed);
        Assert.Equal(info.Id, reclaimed[0].Id);
    }

    /// <summary>
    /// 未到重试时刻的记录不被领取
    /// </summary>
    [Fact]
    public async Task 未到重试时刻的记录不被领取()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);

        var future = DateTimeOffset.UtcNow.AddDays(1);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { NextRetryTime = future })
            .Where(item => item.BasicId == info.Id)
            .ExecuteCommandAsync();

        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 已到重试时刻的记录被领取
    /// </summary>
    [Fact]
    public async Task 已到重试时刻的记录被领取()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);

        var past = DateTimeOffset.UtcNow.AddDays(-1);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { NextRetryTime = past })
            .Where(item => item.BasicId == info.Id)
            .ExecuteCommandAsync();

        var claimed = await context.Inbox.GetWaitingEventsAsync(10);

        Assert.Single(claimed);
        Assert.Equal(info.Id, claimed[0].Id);
    }

    /// <summary>
    /// 已处理与已丢弃的记录不被领取
    /// </summary>
    [Fact]
    public async Task 已处理与已丢弃的记录不被领取()
    {
        using var context = new InboxTestContext();
        var processed = NewEvent();
        var discarded = NewEvent();
        await context.Inbox.EnqueueAsync(processed);
        await context.Inbox.EnqueueAsync(discarded);

        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { Status = SysEventInbox.StatusProcessed })
            .Where(item => item.BasicId == processed.Id)
            .ExecuteCommandAsync();
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { Status = SysEventInbox.StatusDiscarded })
            .Where(item => item.BasicId == discarded.Id)
            .ExecuteCommandAsync();

        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 按创建时间升序领取且不超过上限
    /// </summary>
    [Fact]
    public async Task 按创建时间升序领取且不超过上限()
    {
        using var context = new InboxTestContext();
        var baseTime = DateTime.UtcNow.AddDays(-2);

        for (var index = 4; index >= 0; index--)
        {
            await context.Inbox.EnqueueAsync(new IncomingEventInfo(
                Guid.NewGuid(),
                Guid.NewGuid().ToString("N"),
                "Order.Paid",
                [(byte)index],
                baseTime.AddMinutes(index)));
        }

        var claimed = await context.Inbox.GetWaitingEventsAsync(3);

        Assert.Equal(3, claimed.Count);
        Assert.Equal(new[] { 0, 1, 2 }, claimed.Select(item => (int)item.EventData[0]).ToArray());
    }

    /// <summary>
    /// 过滤条件不为空时抛出不支持
    /// </summary>
    [Fact]
    public async Task 过滤条件不为空时抛出不支持()
    {
        using var context = new InboxTestContext();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Inbox.GetWaitingEventsAsync(10, item => item.EventName == "Order.Paid"));
    }

    /// <summary>
    /// 上限非正时返回空且不领取
    /// </summary>
    [Fact]
    public async Task 上限非正时返回空且不领取()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);

        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(0));

        var stored = await context.Client.Queryable<SysEventInbox>()
            .Where(item => item.BasicId == info.Id)
            .FirstAsync();

        Assert.Equal(SysEventInbox.StatusPending, stored.Status);
    }

    /// <summary>
    /// 已取消的令牌抛出取消异常
    /// </summary>
    [Fact]
    public async Task 已取消的令牌抛出取消异常()
    {
        using var context = new InboxTestContext();
        await context.Inbox.EnqueueAsync(NewEvent());

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Inbox.GetWaitingEventsAsync(10, cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// 租户上下文中领取仍读宿主主库
    /// </summary>
    [Fact]
    public async Task 租户上下文中领取仍读宿主主库()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);

        List<IncomingEventInfo> claimed;
        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            claimed = await context.Inbox.GetWaitingEventsAsync(10);
        }

        Assert.Single(claimed);
        Assert.Equal(info.Id, claimed[0].Id);
    }

    private static IncomingEventInfo NewEvent()
    {
        return new IncomingEventInfo(Guid.NewGuid(), Guid.NewGuid().ToString("N"), "Order.Paid", [1], DateTime.UtcNow);
    }
}
