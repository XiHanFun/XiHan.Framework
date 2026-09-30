// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱领取总量、来源定位删除与待送数统计测试
/// </summary>
public class OutboxSourceTests
{
    /// <summary>
    /// 库数多于上限时单批总量仍不超过上限
    /// </summary>
    [Fact]
    public async Task 库数多于上限时单批总量仍不超过上限()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        await InsertAsync(context.Client, 3);
        await InsertAsync(context.ModuleClient, 3);

        var claimed = await context.Outbox.GetWaitingEventsAsync(1);

        Assert.Single(claimed);
    }

    /// <summary>
    /// 某个库领不满时余量顺延给其他库
    /// </summary>
    [Fact]
    public async Task 某个库领不满时余量顺延给其他库()
    {
        for (var round = 0; round < 2; round++)
        {
            using var context = new OutboxTestContext(withModuleDatabase: true);
            await InsertAsync(context.Client, 1);
            await InsertAsync(context.ModuleClient, 10);

            var claimed = await context.Outbox.GetWaitingEventsAsync(4);

            Assert.Equal(4, claimed.Count);
        }
    }

    /// <summary>
    /// 完成按领取来源删除，不受当前租户上下文影响
    /// </summary>
    [Fact]
    public async Task 完成按领取来源删除不受当前租户影响()
    {
        using var context = new OutboxTestContext(tenantIds: [1001]);
        var info = NewEvent();
        await context.TenantClient(1001).Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();

        List<OutgoingEventInfo> claimed;
        using (context.CurrentTenant.Change(1001))
        {
            claimed = await context.Outbox.GetWaitingEventsAsync(10);
        }

        Assert.Single(claimed);

        // 平台库放一条同标识的记录
        await context.Client.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();

        await context.Outbox.DeleteManyAsync([info.Id]);

        Assert.Equal(0, await context.TenantClient(1001).Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(1, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 记录已被其他实例重新领取时不删除
    /// </summary>
    [Fact]
    public async Task 记录已被其他实例重新领取时不删除()
    {
        using var context = new OutboxTestContext();
        var info = NewEvent();
        await context.Outbox.EnqueueAsync(info);

        Assert.Single(await context.Outbox.GetWaitingEventsAsync(10));

        await context.Client.Updateable<SysEventOutbox>()
            .SetColumns(item => item.ClaimToken == "other-instance")
            .Where(item => item.BasicId == info.Id)
            .ExecuteCommandAsync();

        await context.Outbox.DeleteManyAsync([info.Id]);

        Assert.Equal(1, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 未经本实例领取的标识仍按当前布局删除
    /// </summary>
    [Fact]
    public async Task 未经本实例领取的标识仍按当前布局删除()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        var info = NewEvent();
        await context.ModuleClient.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();

        await context.CreateOutbox().DeleteManyAsync([info.Id]);

        Assert.Equal(0, await context.ModuleClient.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 待送数统计当前布局各库中尚未删除的记录，已领取的也计入
    /// </summary>
    [Fact]
    public async Task 待送数统计当前布局各库中尚未删除的记录()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        await InsertAsync(context.Client, 2);
        await InsertAsync(context.ModuleClient, 1);

        await context.Outbox.GetWaitingEventsAsync(1);

        Assert.Equal(3, await context.Outbox.GetPendingCountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 待送数只统计当前租户的布局
    /// </summary>
    [Fact]
    public async Task 待送数只统计当前租户的布局()
    {
        using var context = new OutboxTestContext(tenantIds: [1001]);
        await InsertAsync(context.TenantClient(1001), 2);
        await InsertAsync(context.Client, 5);

        using (context.CurrentTenant.Change(1001))
        {
            Assert.Equal(2, await context.Outbox.GetPendingCountAsync(TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// 共享布局租户上下文中不领取也不统计平台库的事件
    /// </summary>
    [Fact]
    public async Task 共享布局租户上下文中不领取也不统计平台库的事件()
    {
        using var context = new OutboxTestContext();
        await InsertAsync(context.Client, 3);

        using (context.CurrentTenant.Change(1001))
        {
            Assert.Empty(await context.Outbox.GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(0, await context.Outbox.GetPendingCountAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(3, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 共享布局租户上下文中按标识删除不触及平台库
    /// </summary>
    [Fact]
    public async Task 共享布局租户上下文中按标识删除不触及平台库()
    {
        using var context = new OutboxTestContext();
        var info = NewEvent();
        await context.Client.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();

        using (context.CurrentTenant.Change(1001))
        {
            await context.Outbox.DeleteManyAsync([info.Id]);
        }

        Assert.Equal(1, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 待送数统计在库不可达时抛出，不返回部分结果
    /// </summary>
    [Fact]
    public async Task 待送数统计在库不可达时抛出()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        context.Resolver.FaultyConfigIds[OutboxTestContext.ModuleConfigId] =
            new InvalidOperationException("模拟模块库不可达。");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Outbox.GetPendingCountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task InsertAsync(ISqlSugarClient client, int count)
    {
        var baseTime = DateTime.UtcNow.AddMinutes(-10);

        for (var index = 0; index < count; index++)
        {
            var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], baseTime.AddSeconds(index));
            await client.Insertable(EventOutboxMapper.ToEntity(info)).ExecuteCommandAsync();
        }
    }

    private static OutgoingEventInfo NewEvent()
    {
        return new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1, 2, 3], DateTime.UtcNow);
    }
}
