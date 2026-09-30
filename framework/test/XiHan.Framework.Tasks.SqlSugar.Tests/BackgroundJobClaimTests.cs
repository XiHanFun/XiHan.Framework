// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业领取测试
/// </summary>
public class BackgroundJobClaimTests
{
    /// <summary>
    /// 只领取到期未放弃且应用名匹配的作业
    /// </summary>
    [Fact]
    public async Task 只领取到期未放弃且应用名匹配的作业()
    {
        using var context = new TasksTestContext();
        var due = NewJob("Shop");
        var future = NewJob("Shop", TasksTestContext.BaseTime.AddMinutes(10));
        var abandoned = NewJob("Shop");
        abandoned.IsAbandoned = true;
        var otherApplication = NewJob("Crm");
        var noApplication = NewJob(null);

        foreach (var job in new[] { due, future, abandoned, otherApplication, noApplication })
        {
            await context.BackgroundJobStore.InsertAsync(job);
        }

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync("Shop", 10);

        var single = Assert.Single(claimed);
        Assert.Equal(due.Id, single.Id);
    }

    /// <summary>
    /// 应用名为空时只领取应用名为空的作业
    /// </summary>
    [Fact]
    public async Task 应用名为空时只领取应用名为空的作业()
    {
        using var context = new TasksTestContext();
        var noApplication = NewJob(null);
        await context.BackgroundJobStore.InsertAsync(noApplication);
        await context.BackgroundJobStore.InsertAsync(NewJob("Shop"));

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10);

        var single = Assert.Single(claimed);
        Assert.Equal(noApplication.Id, single.Id);
        Assert.Null(single.ApplicationName);
    }

    /// <summary>
    /// 按优先级降序重试次数升序下次执行时间升序排序
    /// </summary>
    [Fact]
    public async Task 按优先级降序重试次数升序下次执行时间升序排序()
    {
        using var context = new TasksTestContext();
        var low = NewJob(null);
        low.Priority = BackgroundJobPriority.Low;
        var highRetried = NewJob(null);
        highRetried.Priority = BackgroundJobPriority.High;
        highRetried.TryCount = 2;
        var highLater = NewJob(null, TasksTestContext.BaseTime.AddMinutes(-5));
        highLater.Priority = BackgroundJobPriority.High;
        var highEarlier = NewJob(null, TasksTestContext.BaseTime.AddMinutes(-10));
        highEarlier.Priority = BackgroundJobPriority.High;

        foreach (var job in new[] { low, highRetried, highLater, highEarlier })
        {
            await context.BackgroundJobStore.InsertAsync(job);
        }

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10);

        Guid[] expected = [highEarlier.Id, highLater.Id, highRetried.Id, low.Id];
        Assert.Equal(expected, claimed.Select(job => job.Id));
    }

    /// <summary>
    /// 最多返回指定数量
    /// </summary>
    [Fact]
    public async Task 最多返回指定数量()
    {
        using var context = new TasksTestContext();
        for (var index = 0; index < 5; index++)
        {
            await context.BackgroundJobStore.InsertAsync(NewJob(null));
        }

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 3);

        Assert.Equal(3, claimed.Count);
    }

    /// <summary>
    /// 请求数量超过批量上限时只领取上限数量
    /// </summary>
    [Fact]
    public async Task 请求数量超过批量上限时只领取上限数量()
    {
        using var context = new TasksTestContext();
        for (var index = 0; index < 60; index++)
        {
            await context.BackgroundJobStore.InsertAsync(NewJob(null));
        }

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 1000);

        Assert.Equal(50, claimed.Count);
        Assert.Equal(50, await context.Client.Queryable<SysBackgroundJob>()
            .Where(item => item.ClaimToken != null)
            .CountAsync());
    }

    /// <summary>
    /// 批量上限取自配置
    /// </summary>
    [Fact]
    public async Task 批量上限取自配置()
    {
        using var context = new TasksTestContext(maxClaimBatchSize: 7);
        for (var index = 0; index < 20; index++)
        {
            await context.BackgroundJobStore.InsertAsync(NewJob(null));
        }

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 1000);

        Assert.Equal(7, claimed.Count);
    }

    /// <summary>
    /// 数量上限为零时返回空且不领取
    /// </summary>
    [Fact]
    public async Task 数量上限为零时返回空且不领取()
    {
        using var context = new TasksTestContext();
        await context.BackgroundJobStore.InsertAsync(NewJob(null));

        Assert.Empty(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 0));
        Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 已领取且租约未过期的作业不会被再次领取
    /// </summary>
    [Fact]
    public async Task 已领取且租约未过期的作业不会被再次领取()
    {
        using var context = new TasksTestContext();
        await context.BackgroundJobStore.InsertAsync(NewJob(null));

        Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = TasksTestContext.BaseTime.AddMinutes(4);

        Assert.Empty(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 租约过期后作业可被再次领取
    /// </summary>
    [Fact]
    public async Task 租约过期后作业可被再次领取()
    {
        using var context = new TasksTestContext();
        var job = NewJob(null);
        await context.BackgroundJobStore.InsertAsync(job);
        await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10);

        context.Clock.Now = TasksTestContext.BaseTime.AddMinutes(6);

        var claimed = await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10);

        var single = Assert.Single(claimed);
        Assert.Equal(job.Id, single.Id);
    }

    /// <summary>
    /// 更新后释放租约并在下次执行时间到期后可再次领取
    /// </summary>
    [Fact]
    public async Task 更新后释放租约并在下次执行时间到期后可再次领取()
    {
        using var context = new TasksTestContext();
        await context.BackgroundJobStore.InsertAsync(NewJob(null));

        var job = Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
        job.TryCount++;
        job.LastTryTime = context.Clock.Now;
        job.NextTryTime = context.Clock.Now.AddMinutes(2);
        await context.BackgroundJobStore.UpdateAsync(job);

        Assert.Empty(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = TasksTestContext.BaseTime.AddMinutes(3);

        var again = Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
        Assert.Equal(job.Id, again.Id);
        Assert.Equal((short)1, again.TryCount);
    }

    /// <summary>
    /// 领取以注入的时钟为准
    /// </summary>
    [Fact]
    public async Task 领取以注入的时钟为准()
    {
        using var context = new TasksTestContext();
        await context.BackgroundJobStore.InsertAsync(NewJob(null));

        Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));

        var stored = await context.Client.Queryable<SysBackgroundJob>().FirstAsync();
        Assert.NotNull(stored.ClaimToken);
        Assert.True(stored.ClaimTime.HasValue);
        Assert.True(
            Math.Abs((stored.ClaimTime.GetValueOrDefault() - TasksTestContext.BaseTime).TotalSeconds) < 1,
            $"领取时刻应取注入时钟的当前时间，实际 {stored.ClaimTime:O}");
    }

    /// <summary>
    /// 删除后不再被领取
    /// </summary>
    [Fact]
    public async Task 删除后不再被领取()
    {
        using var context = new TasksTestContext();
        var job = NewJob(null);
        await context.BackgroundJobStore.InsertAsync(job);

        await context.BackgroundJobStore.DeleteAsync(job.Id);

        Assert.Empty(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
    }

    private static BackgroundJobInfo NewJob(string? applicationName, DateTime? nextTryTime = null)
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = applicationName,
            JobName = "Order.Close",
            JobArgs = "{\"orderId\":1}",
            CreationTime = TasksTestContext.BaseTime.AddMinutes(-30),
            NextTryTime = nextTryTime ?? TasksTestContext.BaseTime.AddMinutes(-1)
        };
    }
}
