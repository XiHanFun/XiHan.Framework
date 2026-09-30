// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱入箱与判重测试
/// </summary>
public class InboxEnqueueTests
{
    /// <summary>
    /// 入箱后记录为待处理且无领取信息
    /// </summary>
    [Fact]
    public async Task 入箱后记录为待处理且无领取信息()
    {
        using var context = new InboxTestContext();
        var info = NewEvent("msg-enqueue");

        await context.Inbox.EnqueueAsync(info);

        var stored = await context.Client.Queryable<SysEventInbox>()
            .Where(item => item.BasicId == info.Id)
            .FirstAsync();

        Assert.NotNull(stored);
        Assert.Equal(SysEventInbox.StatusPending, stored.Status);
        Assert.Equal("msg-enqueue", stored.MessageId);
        Assert.Equal("msg-enqueue", stored.DedupKey);
        Assert.Equal(0, stored.RetryCount);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.HandledTime);
    }

    /// <summary>
    /// 同一消息标识再次入箱只保留一条且不抛异常
    /// </summary>
    [Fact]
    public async Task 同一消息标识再次入箱只保留一条且不抛异常()
    {
        using var context = new InboxTestContext();
        var first = NewEvent("msg-duplicate");
        var second = NewEvent("msg-duplicate");

        Assert.NotEqual(first.Id, second.Id);

        await context.Inbox.EnqueueAsync(first);
        await context.Inbox.EnqueueAsync(second);

        var stored = await context.Client.Queryable<SysEventInbox>().ToListAsync();

        Assert.Single(stored);
        Assert.Equal(first.Id, stored[0].BasicId);
    }

    /// <summary>
    /// 无消息标识的事件各自入箱
    /// </summary>
    [Fact]
    public async Task 无消息标识的事件各自入箱()
    {
        using var context = new InboxTestContext();

        await context.Inbox.EnqueueAsync(NewEvent(null!));
        await context.Inbox.EnqueueAsync(NewEvent(null!));

        Assert.Equal(2, await context.Client.Queryable<SysEventInbox>().CountAsync());
    }

    /// <summary>
    /// 非重复原因的写入失败原样抛出
    /// </summary>
    [Fact]
    public async Task 非重复原因的写入失败原样抛出()
    {
        using var context = new InboxTestContext();
        context.Client.Ado.ExecuteCommand(
            "CREATE TRIGGER trg_reject_inbox BEFORE INSERT ON sys_event_inbox BEGIN SELECT RAISE(ABORT, 'inbox-rejected'); END;");

        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Inbox.EnqueueAsync(NewEvent("msg-rejected")));

        Assert.Contains("inbox-rejected", error.ToString());
    }

    /// <summary>
    /// 租户上下文中入箱仍写宿主主库
    /// </summary>
    [Fact]
    public async Task 租户上下文中入箱仍写宿主主库()
    {
        using var context = new InboxTestContext();

        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            await context.Inbox.EnqueueAsync(NewEvent("msg-tenant"));
        }

        Assert.Equal(1, await context.Client.Queryable<SysEventInbox>().CountAsync());
        Assert.Equal(0, await context.TenantClient.Queryable<SysEventInbox>().CountAsync());
    }

    /// <summary>
    /// 入箱后恢复调用方的租户上下文
    /// </summary>
    [Fact]
    public async Task 入箱后恢复调用方的租户上下文()
    {
        using var context = new InboxTestContext();

        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            await context.Inbox.EnqueueAsync(NewEvent("msg-restore"));

            Assert.Equal(InboxTestContext.TenantId, context.CurrentTenant.Id);
        }
    }

    /// <summary>
    /// 已收录的消息标识判定为存在
    /// </summary>
    [Fact]
    public async Task 已收录的消息标识判定为存在()
    {
        using var context = new InboxTestContext();
        await context.Inbox.EnqueueAsync(NewEvent("msg-exists"));

        Assert.True(await context.Inbox.ExistsByMessageIdAsync("msg-exists"));
        Assert.False(await context.Inbox.ExistsByMessageIdAsync("msg-unknown"));
    }

    /// <summary>
    /// 空白消息标识判定为不存在
    /// </summary>
    /// <param name="messageId">空白的消息标识</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task 空白消息标识判定为不存在(string messageId)
    {
        using var context = new InboxTestContext();

        Assert.False(await context.Inbox.ExistsByMessageIdAsync(messageId));
    }

    /// <summary>
    /// 租户上下文中判重仍查宿主主库
    /// </summary>
    [Fact]
    public async Task 租户上下文中判重仍查宿主主库()
    {
        using var context = new InboxTestContext();
        await context.Inbox.EnqueueAsync(NewEvent("msg-host"));

        using (context.CurrentTenant.Change(InboxTestContext.TenantId))
        {
            Assert.True(await context.Inbox.ExistsByMessageIdAsync("msg-host"));
        }
    }

    private static IncomingEventInfo NewEvent(string messageId)
    {
        return new IncomingEventInfo(Guid.NewGuid(), messageId, "Order.Paid", [1, 2, 3], DateTime.UtcNow);
    }
}
