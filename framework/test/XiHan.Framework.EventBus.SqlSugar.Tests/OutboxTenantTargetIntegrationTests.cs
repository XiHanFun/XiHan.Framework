// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 租户独立库发件箱的端到端测试：两个租户各自一个 SQLite 库
/// </summary>
public class OutboxTenantTargetIntegrationTests
{
    /// <summary>
    /// 两个租户独立库的事件各自入箱并被投递
    /// </summary>
    [Fact]
    public async Task 两个租户独立库的事件各自入箱并被投递()
    {
        using var context = NewContext(1001, 1002);
        using var host = OutboxTenantHost.For(context);

        var first = await host.EnqueueAsync(1001);
        var second = await host.EnqueueAsync(1002);

        Assert.Equal(1, await CountAsync(context, 1001));
        Assert.Equal(1, await CountAsync(context, 1002));
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());

        var claimed = await host.RoundAsync();

        Assert.Equal(2, claimed);
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(id => id),
            host.Bus.Published.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(0, await CountAsync(context, 1001));
        Assert.Equal(0, await CountAsync(context, 1002));
        Assert.Null(context.CurrentTenant.Id);
    }

    /// <summary>
    /// 重启后由目录重建并投递
    /// </summary>
    [Fact]
    public async Task 重启后由目录重建并投递()
    {
        using var context = NewContext(1001);

        OutgoingEventInfo info;
        using (var before = OutboxTenantHost.For(context))
        {
            info = await before.EnqueueAsync(1001);
        }

        using var after = OutboxTenantHost.For(context);
        await after.RoundAsync();

        Assert.Contains(after.Bus.Published, item => item.Id == info.Id);
        Assert.Equal(0, await CountAsync(context, 1001));
    }

    /// <summary>
    /// 停用目标继续排空且拒绝新入箱，待送数归零
    /// </summary>
    [Fact]
    public async Task 停用目标继续排空且拒绝新入箱()
    {
        using var context = NewContext(1001);
        using var host = OutboxTenantHost.For(context);
        await host.EnqueueAsync(1001);
        await host.EnqueueAsync(1001);

        context.TargetProvider!.SetEnabled(1001, false);
        var target = new OutboxDeliveryTarget(1001, isEnabled: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.EnqueueAsync(1001));
        Assert.Equal(2, await host.Counter.GetPendingCountAsync(target, TestContext.Current.CancellationToken));

        await host.RoundAsync();

        Assert.Equal(0, await host.Counter.GetPendingCountAsync(target, TestContext.Current.CancellationToken));
        Assert.Equal(2, host.Bus.Published.Count);
    }

    /// <summary>
    /// 单轮领取总数不超过批量上限
    /// </summary>
    [Fact]
    public async Task 单轮领取总数不超过批量上限()
    {
        using var context = NewContext(1001, 1002);
        using var host = OutboxTenantHost.For(context, batchSize: 5);
        await InsertAsync(context.Client, 4);
        await InsertAsync(context.TenantClient(1001), 4);
        await InsertAsync(context.TenantClient(1002), 4);

        var claimed = await host.RoundAsync();

        Assert.Equal(5, claimed);
        Assert.Equal(5, host.Bus.Published.Count);
        Assert.Equal(
            7,
            await context.Client.Queryable<SysEventOutbox>().CountAsync() +
            await CountAsync(context, 1001) +
            await CountAsync(context, 1002));
    }

    /// <summary>
    /// 热点租户不阻塞其他租户
    /// </summary>
    [Fact]
    public async Task 热点租户不阻塞其他租户()
    {
        using var context = NewContext(1001, 1002);
        using var host = OutboxTenantHost.For(context, batchSize: 5);
        await InsertAsync(context.TenantClient(1001), 30);
        await InsertAsync(context.TenantClient(1002), 1);

        await host.RoundAsync();
        Assert.Equal(1, await CountAsync(context, 1002));
        await host.RoundAsync();

        Assert.Equal(0, await CountAsync(context, 1002));
    }

    /// <summary>
    /// 故障租户库不阻塞健康租户
    /// </summary>
    [Fact]
    public async Task 故障租户库不阻塞健康租户()
    {
        using var context = NewContext(1001, 1002);
        using var host = OutboxTenantHost.For(context);
        await InsertAsync(context.TenantClient(1001), 1);
        await InsertAsync(context.TenantClient(1002), 1);
        context.Resolver.FaultyConfigIds[OutboxTestContext.TenantConfigId(1001)] =
            new InvalidOperationException("模拟租户库不可达。");

        await host.RoundAsync();

        Assert.Equal(0, await CountAsync(context, 1002));
        context.Resolver.FaultyConfigIds.Clear();
        Assert.Equal(1, await CountAsync(context, 1001));
    }

    /// <summary>
    /// 目录同时列出独立库租户与共享布局租户时平台库只在宿主上下文中投递一次
    /// </summary>
    [Fact]
    public async Task 共享布局租户不重复扫描平台库()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));
        context.TargetProvider.Targets.Add(new OutboxDeliveryTarget(2002));
        using var host = OutboxTenantHost.For(context);
        host.Bus.TenantProbe = () => context.CurrentTenant.Id;
        await InsertAsync(context.Client, 3);
        await InsertAsync(context.TenantClient(1001), 1);
        var platformIds = (await context.Client.Queryable<SysEventOutbox>().Select(item => item.BasicId).ToListAsync()).ToHashSet();

        Assert.Equal(0, await host.Counter.GetPendingCountAsync(new OutboxDeliveryTarget(2002), TestContext.Current.CancellationToken));

        var claimed = await host.RoundAsync();

        Assert.Equal(4, claimed);
        Assert.Equal(4, host.Bus.Published.Count);
        Assert.Equal(4, host.Bus.Published.Select(item => item.Id).Distinct().Count());
        Assert.All(
            host.Bus.PublishedTenants.Where(item => platformIds.Contains(item.EventId)),
            item => Assert.Null(item.TenantId));
        Assert.Equal(3, host.Bus.PublishedTenants.Count(item => platformIds.Contains(item.EventId)));
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(0, await CountAsync(context, 1001));
    }

    private static OutboxTestContext NewContext(params long[] tenantIds)
    {
        var context = new OutboxTestContext(tenantIds: tenantIds, withTargetProvider: true);
        context.TargetProvider!.Targets.AddRange(tenantIds.Select(tenantId => new OutboxDeliveryTarget(tenantId)));
        return context;
    }

    private static Task<int> CountAsync(OutboxTestContext context, long tenantId)
    {
        return context.TenantClient(tenantId).Queryable<SysEventOutbox>().CountAsync();
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
}
