// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业管理操作（重试与取消）的存储测试
/// </summary>
public class BackgroundJobManagementStoreTests
{
    private static readonly DateTime Now = TasksTestContext.BaseTime;

    /// <summary>
    /// 重试已放弃的作业后重新待执行，再次重试无变化
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 重试已放弃的作业后重新待执行且再次重试无变化()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        job.IsAbandoned = true;
        job.TryCount = 5;
        job.NextTryTime = Now.AddDays(1);
        await store.InsertAsync(job);

        Assert.Equal(BackgroundJobManagementStatus.Rescheduled, await store.RetryAbandonedAsync(job.Id, cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.NoChange, await store.RetryAbandonedAsync(job.Id, cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.False(found.IsAbandoned);
        Assert.False(found.IsCancellationRequested);
        Assert.Equal((short)0, found.TryCount);

        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.Equal(job.Id, claimed.Id);
    }

    /// <summary>
    /// 取消后的作业可以重试，取消标记被清除
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消后的作业可以重试且取消标记被清除()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, await store.RequestCancellationAsync(job.Id, cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.Rescheduled, await store.RetryAbandonedAsync(job.Id, cancellationToken));

        var stored = await FindEntityAsync(context, job.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsAbandoned);
        Assert.Null(stored.IsCancellationRequested);
    }

    /// <summary>
    /// 重试未放弃的作业无变化，重试不存在的作业返回未找到
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 重试未放弃或不存在的作业()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);

        Assert.Equal(BackgroundJobManagementStatus.NoChange, await store.RetryAbandonedAsync(job.Id, cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.NotFound, await store.RetryAbandonedAsync(Guid.NewGuid(), cancellationToken));
    }

    /// <summary>
    /// 取消等待中的作业直接放弃，作业保留在表中
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消等待中的作业直接放弃并保留()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, await store.RequestCancellationAsync(job.Id, cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.NoChange, await store.RequestCancellationAsync(job.Id, cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.True(found.IsAbandoned);
        Assert.True(found.IsCancellationRequested);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 取消持有有效租约的作业登记取消请求，续租携带该标记，重复请求无变化
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消持租中的作业登记取消请求且重复请求无变化()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, await store.RequestCancellationAsync(job.Id, cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.NoChange, await store.RequestCancellationAsync(job.Id, cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.False(found.IsAbandoned);
        Assert.True(found.IsCancellationRequested);
        Assert.Equal(claimed.ClaimToken, found.ClaimToken);

        var renewed = await store.TryRenewLeaseAsync(
            new BackgroundJobLease(claimed.Id, claimed.ClaimToken!, claimed.LeaseExpiresAt!.Value),
            cancellationToken);
        Assert.NotNull(renewed);
        Assert.True(renewed.IsCancellationRequested);
    }

    /// <summary>
    /// 租约已过期的作业视为未在执行，取消直接放弃
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消租约已过期的作业直接放弃()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(6);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, await store.RequestCancellationAsync(job.Id, cancellationToken));

        var stored = await FindEntityAsync(context, job.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsAbandoned);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.ClaimTime);
    }

    /// <summary>
    /// 取消不存在的作业返回未找到，取消已放弃的作业无变化
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消不存在或已放弃的作业()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        job.IsAbandoned = true;
        await store.InsertAsync(job);

        Assert.Equal(BackgroundJobManagementStatus.NotFound, await store.RequestCancellationAsync(Guid.NewGuid(), cancellationToken));
        Assert.Equal(BackgroundJobManagementStatus.NoChange, await store.RequestCancellationAsync(job.Id, cancellationToken));
    }

    private static Task<SysBackgroundJob> FindEntityAsync(TasksTestContext context, Guid jobId)
    {
        return context.Client.Queryable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .FirstAsync();
    }

    private static BackgroundJobInfo NewJob()
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "Order.Close",
            JobArgs = "{\"orderId\":1}",
            CreationTime = Now.AddMinutes(-30),
            NextTryTime = Now.AddMinutes(-1)
        };
    }
}
