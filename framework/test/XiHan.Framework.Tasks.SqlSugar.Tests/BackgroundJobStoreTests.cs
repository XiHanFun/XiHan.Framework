// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业存储增删改查测试
/// </summary>
public class BackgroundJobStoreTests
{
    /// <summary>
    /// 插入后按标识查回字段一致
    /// </summary>
    [Fact]
    public async Task 插入后按标识查回字段一致()
    {
        using var context = new TasksTestContext();
        var lastTryTime = TasksTestContext.BaseTime.AddMinutes(-5);
        var job = NewJob();
        job.ApplicationName = "Shop";
        job.TenantId = 42;
        job.TryCount = 2;
        job.LastTryTime = lastTryTime;
        job.Priority = BackgroundJobPriority.High;

        await context.BackgroundJobStore.InsertAsync(job);
        var found = await context.BackgroundJobStore.FindAsync(job.Id);

        Assert.NotNull(found);
        Assert.Equal(job.Id, found.Id);
        Assert.Equal("Shop", found.ApplicationName);
        Assert.Equal(42L, found.TenantId);
        Assert.Equal(job.JobName, found.JobName);
        Assert.Equal(job.JobArgs, found.JobArgs);
        Assert.Equal((short)2, found.TryCount);
        Assert.Equal(BackgroundJobPriority.High, found.Priority);
        Assert.False(found.IsAbandoned);
        AssertClose(job.CreationTime, found.CreationTime);
        AssertClose(job.NextTryTime, found.NextTryTime);
        Assert.True(found.LastTryTime.HasValue);
        AssertClose(lastTryTime, found.LastTryTime.GetValueOrDefault());
    }

    /// <summary>
    /// 查找不存在的作业返回空
    /// </summary>
    [Fact]
    public async Task 查找不存在的作业返回空()
    {
        using var context = new TasksTestContext();

        Assert.Null(await context.BackgroundJobStore.FindAsync(Guid.NewGuid()));
    }

    /// <summary>
    /// 插入空作业抛出参数异常
    /// </summary>
    [Fact]
    public async Task 插入空作业抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.BackgroundJobStore.InsertAsync(null!));
    }

    /// <summary>
    /// 更新空作业抛出参数异常
    /// </summary>
    [Fact]
    public async Task 更新空作业抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.BackgroundJobStore.UpdateAsync(null!));
    }

    /// <summary>
    /// 删除后查不到且重复删除不抛异常
    /// </summary>
    [Fact]
    public async Task 删除后查不到且重复删除不抛异常()
    {
        using var context = new TasksTestContext();
        var job = NewJob();
        await context.BackgroundJobStore.InsertAsync(job);

        await context.BackgroundJobStore.DeleteAsync(job.Id);
        await context.BackgroundJobStore.DeleteAsync(job.Id);

        Assert.Null(await context.BackgroundJobStore.FindAsync(job.Id));
    }

    /// <summary>
    /// 更新覆盖失败回写的字段
    /// </summary>
    [Fact]
    public async Task 更新覆盖失败回写的字段()
    {
        using var context = new TasksTestContext();
        var job = NewJob();
        await context.BackgroundJobStore.InsertAsync(job);

        var nextTryTime = TasksTestContext.BaseTime.AddMinutes(1);
        job.TryCount = 1;
        job.LastTryTime = TasksTestContext.BaseTime;
        job.NextTryTime = nextTryTime;
        await context.BackgroundJobStore.UpdateAsync(job);

        var found = await context.BackgroundJobStore.FindAsync(job.Id);

        Assert.NotNull(found);
        Assert.Equal((short)1, found.TryCount);
        AssertClose(nextTryTime, found.NextTryTime);
        Assert.True(found.LastTryTime.HasValue);
    }

    /// <summary>
    /// 放弃的作业保留在表中并标记放弃
    /// </summary>
    [Fact]
    public async Task 放弃的作业保留在表中并标记放弃()
    {
        using var context = new TasksTestContext();
        var job = NewJob();
        await context.BackgroundJobStore.InsertAsync(job);

        job.IsAbandoned = true;
        await context.BackgroundJobStore.UpdateAsync(job);

        var found = await context.BackgroundJobStore.FindAsync(job.Id);

        Assert.NotNull(found);
        Assert.True(found.IsAbandoned);
    }

    /// <summary>
    /// 更新不存在的作业不会插入
    /// </summary>
    [Fact]
    public async Task 更新不存在的作业不会插入()
    {
        using var context = new TasksTestContext();

        await context.BackgroundJobStore.UpdateAsync(NewJob());

        Assert.Equal(0, await context.Client.Queryable<SysBackgroundJob>().CountAsync());
    }

    /// <summary>
    /// 在租户上下文中入队时以宿主上下文写库并保留作业租户
    /// </summary>
    [Fact]
    public async Task 在租户上下文中入队时以宿主上下文写库并保留作业租户()
    {
        using var context = new TasksTestContext();
        var job = NewJob();
        job.TenantId = 42;

        using (context.Tenant.Change(42))
        {
            await context.BackgroundJobStore.InsertAsync(job);
        }

        Assert.NotEmpty(context.ExecutingTenantIds);
        Assert.All(context.ExecutingTenantIds, tenantId => Assert.Null(tenantId));
        Assert.NotEmpty(context.Resolver.ObservedTenantIds);
        Assert.All(context.Resolver.ObservedTenantIds, tenantId => Assert.Null(tenantId));

        var stored = await context.Client.Queryable<SysBackgroundJob>().FirstAsync();
        Assert.Equal(42L, stored.TenantId);
    }

    private static BackgroundJobInfo NewJob()
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "Order.Close",
            JobArgs = "{\"orderId\":1}",
            CreationTime = TasksTestContext.BaseTime.AddMinutes(-30),
            NextTryTime = TasksTestContext.BaseTime.AddMinutes(-1)
        };
    }

    private static void AssertClose(DateTime expected, DateTime actual)
    {
        Assert.True(
            Math.Abs((expected - actual).TotalSeconds) < 1,
            $"期望 {expected:O}，实际 {actual:O}");
    }
}
