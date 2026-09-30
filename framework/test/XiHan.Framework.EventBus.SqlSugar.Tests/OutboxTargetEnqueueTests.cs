// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Outbox;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱入箱落点与投递目标检查测试
/// </summary>
public class OutboxTargetEnqueueTests
{
    /// <summary>
    /// 指定已登记的连接时事件写入该库
    /// </summary>
    [Fact]
    public async Task 指定已登记的连接时事件写入该库()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        context.Resolver.EnlistedConfigIds.Add(OutboxTestContext.MainConfigId);
        context.Resolver.EnlistedConfigIds.Add(OutboxTestContext.ModuleConfigId);

        using (context.ConnectionScope.Use(OutboxTestContext.ModuleConfigId))
        {
            await context.Outbox.EnqueueAsync(NewEvent());
        }

        Assert.Equal(1, await context.ModuleClient.Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 指定未登记的连接时拒绝且不写库
    /// </summary>
    [Fact]
    public async Task 指定未登记的连接时拒绝且不写库()
    {
        using var context = new OutboxTestContext(withModuleDatabase: true);
        context.Resolver.EnlistedConfigIds.Add(OutboxTestContext.MainConfigId);

        using (context.ConnectionScope.Use(OutboxTestContext.ModuleConfigId))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));

            Assert.Contains("未登记", error.Message);
        }

        Assert.Equal(0, await context.ModuleClient.Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 没有登记任何连接时指定连接也被拒绝
    /// </summary>
    [Fact]
    public async Task 没有登记任何连接时指定连接也被拒绝()
    {
        using var context = new OutboxTestContext();

        using (context.ConnectionScope.Use(OutboxTestContext.MainConfigId))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));
        }

        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 连接范围释放后恢复外层指定
    /// </summary>
    [Fact]
    public void 连接范围释放后恢复外层指定()
    {
        var scope = new AsyncLocalSqlSugarOutboxConnectionScope();

        using (scope.Use("A"))
        {
            using (scope.Use(" B "))
            {
                Assert.Equal("B", scope.ConfigId);
            }

            Assert.Equal("A", scope.ConfigId);
        }

        Assert.Null(scope.ConfigId);
    }

    /// <summary>
    /// 连接范围不接受空白标识
    /// </summary>
    [Fact]
    public void 连接范围不接受空白标识()
    {
        var scope = new AsyncLocalSqlSugarOutboxConnectionScope();

        Assert.ThrowsAny<ArgumentException>(() => scope.Use(" "));
    }

    /// <summary>
    /// 目录中启用的独立库租户入箱写入租户库
    /// </summary>
    [Fact]
    public async Task 目录中启用的独立库租户入箱写入租户库()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));

        using (context.CurrentTenant.Change(1001))
        {
            await context.Outbox.EnqueueAsync(NewEvent());
        }

        Assert.Equal(1, await context.TenantClient(1001).Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 不在目录中的独立库租户入箱被拒绝
    /// </summary>
    [Fact]
    public async Task 不在目录中的独立库租户入箱被拒绝()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);

        using (context.CurrentTenant.Change(1001))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));

            Assert.Contains("目录", error.Message);
        }

        Assert.Equal(0, await context.TenantClient(1001).Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 目录中停用的独立库租户入箱被拒绝
    /// </summary>
    [Fact]
    public async Task 目录中停用的独立库租户入箱被拒绝()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001, isEnabled: false));

        using (context.CurrentTenant.Change(1001))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));

            Assert.Contains("停用", error.Message);
        }

        Assert.Equal(0, await context.TenantClient(1001).Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 目录中停用的共享布局租户入箱也被拒绝
    /// </summary>
    [Fact]
    public async Task 目录中停用的共享布局租户入箱也被拒绝()
    {
        using var context = new OutboxTestContext(withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001, isEnabled: false));

        using (context.CurrentTenant.Change(1001))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));

            Assert.Contains("停用", error.Message);
        }

        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 不在目录中的共享布局租户照常入箱
    /// </summary>
    [Fact]
    public async Task 不在目录中的共享布局租户照常入箱()
    {
        using var context = new OutboxTestContext(withTargetProvider: true);

        using (context.CurrentTenant.Change(1001))
        {
            await context.Outbox.EnqueueAsync(NewEvent());
        }

        Assert.Equal(1, await context.Client.Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 落点既不在平台布局也不在租户布局时拒绝
    /// </summary>
    [Fact]
    public async Task 落点既不在平台布局也不在租户布局时拒绝()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));
        context.Resolver.EnlistedConfigIds.Add("Unknown_Db");

        using (context.CurrentTenant.Change(1001))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.Outbox.EnqueueAsync(NewEvent()));

            Assert.Contains("既不在平台布局", error.Message);
        }
    }

    /// <summary>
    /// 租户库事务回滚后事件不可见
    /// </summary>
    [Fact]
    public async Task 租户库事务回滚后事件不可见()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));
        var tenantClient = context.TenantClient(1001);

        tenantClient.Ado.BeginTran();
        using (context.CurrentTenant.Change(1001))
        {
            await context.Outbox.EnqueueAsync(NewEvent());
        }
        Assert.Equal(1, await tenantClient.Queryable<SysEventOutbox>().CountAsync());
        tenantClient.Ado.RollbackTran();

        Assert.Equal(0, await tenantClient.Queryable<SysEventOutbox>().CountAsync());
        Assert.Equal(0, await context.Client.Queryable<SysEventOutbox>().CountAsync());

        using (context.CurrentTenant.Change(1001))
        {
            Assert.Empty(await context.Outbox.GetWaitingEventsAsync(10));
        }
    }

    /// <summary>
    /// 目录查询在无租户上下文中进行
    /// </summary>
    [Fact]
    public async Task 目录查询在无租户上下文中进行()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));
        context.TargetProvider.TenantProbe = () => context.CurrentTenant.Id;

        using (context.CurrentTenant.Change(1001))
        {
            await context.Outbox.EnqueueAsync(NewEvent());

            Assert.Equal(1001, context.CurrentTenant.Id);
        }

        Assert.Equal([(long?)null], context.TargetProvider.FindTenantContexts);
        Assert.Equal(1, await context.TenantClient(1001).Queryable<SysEventOutbox>().CountAsync());
    }

    /// <summary>
    /// 目录查询在独立的非事务工作单元中进行且随后释放
    /// </summary>
    [Fact]
    public async Task 目录查询在独立的非事务工作单元中进行且随后释放()
    {
        using var context = new OutboxTestContext(tenantIds: [1001], withTargetProvider: true);
        var manager = new RecordingUnitOfWorkManager();
        context.UnitOfWorkManager = manager;
        context.TargetProvider!.Targets.Add(new OutboxDeliveryTarget(1001));
        var openDuringFind = new List<bool>();
        context.TargetProvider.TenantProbe = () =>
        {
            openDuringFind.Add(manager.BeginCalls.Count == 1 && !manager.BeginCalls[0].UnitOfWork.IsDisposed);
            return null;
        };
        var outbox = context.CreateOutbox();

        using (context.CurrentTenant.Change(1001))
        {
            await outbox.EnqueueAsync(NewEvent());
        }

        var call = Assert.Single(manager.BeginCalls);
        Assert.True(call.RequiresNew);
        Assert.False(call.IsTransactional);
        Assert.True(call.UnitOfWork.IsCompleted);
        Assert.True(call.UnitOfWork.IsDisposed);
        Assert.Equal([true], openDuringFind);
    }

    private static OutgoingEventInfo NewEvent()
    {
        return new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1, 2, 3], DateTime.UtcNow);
    }
}
