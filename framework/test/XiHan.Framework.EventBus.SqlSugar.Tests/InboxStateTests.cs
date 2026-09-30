// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱状态流转与清理测试
/// </summary>
public class InboxStateTests
{
    /// <summary>
    /// 实现收件箱契约
    /// </summary>
    [Fact]
    public void 实现收件箱契约()
    {
        using var context = new InboxTestContext();

        Assert.IsAssignableFrom<IEventInbox>(context.Inbox);
    }

    /// <summary>
    /// 标记已处理后不再被领取且仍判定为存在
    /// </summary>
    [Fact]
    public async Task 标记已处理后不再被领取且仍判定为存在()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        await context.Inbox.MarkAsProcessedAsync(info.Id);

        var stored = await FindAsync(context, info.Id);

        Assert.Equal(SysEventInbox.StatusProcessed, stored.Status);
        Assert.NotNull(stored.HandledTime);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.ClaimTime);
        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(10));
        Assert.True(await context.Inbox.ExistsByMessageIdAsync(info.MessageId));
    }

    /// <summary>
    /// 标记丢弃后不再被领取且仍判定为存在
    /// </summary>
    [Fact]
    public async Task 标记丢弃后不再被领取且仍判定为存在()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        await context.Inbox.MarkAsDiscardAsync(info.Id);

        var stored = await FindAsync(context, info.Id);

        Assert.Equal(SysEventInbox.StatusDiscarded, stored.Status);
        Assert.NotNull(stored.HandledTime);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.ClaimTime);
        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(10));
        Assert.True(await context.Inbox.ExistsByMessageIdAsync(info.MessageId));
    }

    /// <summary>
    /// 延后重试把记录放回待处理并记录次数
    /// </summary>
    [Fact]
    public async Task 延后重试把记录放回待处理并记录次数()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        await context.Inbox.RetryLaterAsync(info.Id, 2, DateTime.UtcNow.AddDays(-1));

        var stored = await FindAsync(context, info.Id);

        Assert.Equal(SysEventInbox.StatusPending, stored.Status);
        Assert.Equal(2, stored.RetryCount);
        Assert.NotNull(stored.NextRetryTime);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.ClaimTime);
        Assert.Null(stored.HandledTime);

        var retried = await context.Inbox.GetWaitingEventsAsync(10);

        Assert.Single(retried);
        Assert.Equal(info.Id, retried[0].Id);
    }

    /// <summary>
    /// 延后重试的时刻未到时暂不领取
    /// </summary>
    [Fact]
    public async Task 延后重试的时刻未到时暂不领取()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        await context.Inbox.RetryLaterAsync(info.Id, 1, DateTime.UtcNow.AddDays(1));

        Assert.Empty(await context.Inbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 延后重试未给时刻时立即可领取
    /// </summary>
    [Fact]
    public async Task 延后重试未给时刻时立即可领取()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));

        await context.Inbox.RetryLaterAsync(info.Id, 1, null);

        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));
    }

    /// <summary>
    /// 已完结的记录不会被延后重试回退
    /// </summary>
    [Fact]
    public async Task 已完结的记录不会被延后重试回退()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));
        await context.Inbox.MarkAsProcessedAsync(info.Id);

        await context.Inbox.RetryLaterAsync(info.Id, 5, null);

        var stored = await FindAsync(context, info.Id);

        Assert.Equal(SysEventInbox.StatusProcessed, stored.Status);
        Assert.Equal(0, stored.RetryCount);
        Assert.NotNull(stored.HandledTime);
    }

    /// <summary>
    /// 已丢弃的记录不会被再次标记为已处理
    /// </summary>
    [Fact]
    public async Task 已丢弃的记录不会被再次标记为已处理()
    {
        using var context = new InboxTestContext();
        var info = NewEvent();
        await context.Inbox.EnqueueAsync(info);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));
        await context.Inbox.MarkAsDiscardAsync(info.Id);

        await context.Inbox.MarkAsProcessedAsync(info.Id);

        Assert.Equal(SysEventInbox.StatusDiscarded, (await FindAsync(context, info.Id)).Status);
    }

    /// <summary>
    /// 对不存在的标识更新状态不抛异常
    /// </summary>
    [Fact]
    public async Task 对不存在的标识更新状态不抛异常()
    {
        using var context = new InboxTestContext();
        var missing = Guid.NewGuid();

        await context.Inbox.MarkAsProcessedAsync(missing);
        await context.Inbox.MarkAsDiscardAsync(missing);
        await context.Inbox.RetryLaterAsync(missing, 1, null);

        Assert.Equal(0, await context.Client.Queryable<SysEventInbox>().CountAsync());
    }

    /// <summary>
    /// 清理只删除超过保留期的已完结记录
    /// </summary>
    [Fact]
    public async Task 清理只删除超过保留期的已完结记录()
    {
        using var context = new InboxTestContext(retentionPeriod: TimeSpan.FromDays(7));
        var processedOld = NewEvent();
        var discardedOld = NewEvent();
        var processedRecent = NewEvent();
        var pendingOld = NewEvent();

        await context.Inbox.EnqueueAsync(processedOld);
        await context.Inbox.EnqueueAsync(discardedOld);
        await context.Inbox.EnqueueAsync(processedRecent);
        await context.Inbox.EnqueueAsync(pendingOld);
        Assert.Equal(4, (await context.Inbox.GetWaitingEventsAsync(10)).Count);

        await context.Inbox.MarkAsProcessedAsync(processedOld.Id);
        await context.Inbox.MarkAsDiscardAsync(discardedOld.Id);
        await context.Inbox.MarkAsProcessedAsync(processedRecent.Id);

        var old = DateTimeOffset.UtcNow.AddDays(-30);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { HandledTime = old })
            .Where(item => item.BasicId == processedOld.Id || item.BasicId == discardedOld.Id)
            .ExecuteCommandAsync();
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { CreatedTime = old })
            .Where(item => item.BasicId == pendingOld.Id)
            .ExecuteCommandAsync();

        await context.Inbox.DeleteOldEventsAsync();

        var remaining = await context.Client.Queryable<SysEventInbox>()
            .Select(item => item.BasicId)
            .ToListAsync();

        Assert.Equal(2, remaining.Count);
        Assert.Contains(processedRecent.Id, remaining);
        Assert.Contains(pendingOld.Id, remaining);
    }

    /// <summary>
    /// 清理后同一消息可再次入箱
    /// </summary>
    [Fact]
    public async Task 清理后同一消息可再次入箱()
    {
        using var context = new InboxTestContext(retentionPeriod: TimeSpan.FromDays(7));
        var first = NewEvent("msg-expired");
        await context.Inbox.EnqueueAsync(first);
        Assert.Single(await context.Inbox.GetWaitingEventsAsync(10));
        await context.Inbox.MarkAsProcessedAsync(first.Id);

        var old = DateTimeOffset.UtcNow.AddDays(-30);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { HandledTime = old })
            .Where(item => item.BasicId == first.Id)
            .ExecuteCommandAsync();

        await context.Inbox.DeleteOldEventsAsync();

        Assert.False(await context.Inbox.ExistsByMessageIdAsync("msg-expired"));

        var second = NewEvent("msg-expired");
        await context.Inbox.EnqueueAsync(second);

        Assert.Equal(second.Id, (await FindAsync(context, second.Id)).BasicId);
    }

    /// <summary>
    /// 租户上下文中状态流转与清理仍作用于宿主主库
    /// </summary>
    [Fact]
    public async Task 租户上下文中状态流转与清理仍作用于宿主主库()
    {
        using var context = new InboxTestContext(retentionPeriod: TimeSpan.FromDays(7));
        var processed = NewEvent();
        var retried = NewEvent();
        var discarded = NewEvent();
        await context.Inbox.EnqueueAsync(processed);
        await context.Inbox.EnqueueAsync(retried);
        await context.Inbox.EnqueueAsync(discarded);
        Assert.Equal(3, (await context.Inbox.GetWaitingEventsAsync(10)).Count);

        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            await context.Inbox.MarkAsProcessedAsync(processed.Id);
            await context.Inbox.RetryLaterAsync(retried.Id, 3, null);
            await context.Inbox.MarkAsDiscardAsync(discarded.Id);
        }

        Assert.Equal(SysEventInbox.StatusProcessed, (await FindAsync(context, processed.Id)).Status);
        Assert.Equal(3, (await FindAsync(context, retried.Id)).RetryCount);
        Assert.Equal(SysEventInbox.StatusDiscarded, (await FindAsync(context, discarded.Id)).Status);

        var old = DateTimeOffset.UtcNow.AddDays(-30);
        await context.Client.Updateable<SysEventInbox>()
            .SetColumns(item => new SysEventInbox { HandledTime = old })
            .Where(item => item.BasicId == processed.Id)
            .ExecuteCommandAsync();

        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            await context.Inbox.DeleteOldEventsAsync();
        }

        Assert.Equal(2, await context.Client.Queryable<SysEventInbox>().CountAsync());
    }

    private static async Task<SysEventInbox> FindAsync(InboxTestContext context, Guid id)
    {
        var stored = await context.Client.Queryable<SysEventInbox>()
            .Where(item => item.BasicId == id)
            .FirstAsync();

        Assert.NotNull(stored);

        return stored;
    }

    private static IncomingEventInfo NewEvent()
    {
        return NewEvent(Guid.NewGuid().ToString("N"));
    }

    private static IncomingEventInfo NewEvent(string messageId)
    {
        return new IncomingEventInfo(Guid.NewGuid(), messageId, "Order.Paid", [1], DateTime.UtcNow);
    }
}
