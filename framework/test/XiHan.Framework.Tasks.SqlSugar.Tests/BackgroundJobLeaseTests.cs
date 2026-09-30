// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业逐作业租约测试
/// </summary>
public class BackgroundJobLeaseTests
{
    private static readonly DateTime Now = TasksTestContext.BaseTime;

    /// <summary>
    /// 存储声明支持租约与管理
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 存储声明支持租约与管理()
    {
        using var context = new TasksTestContext();
        IBackgroundJobStore store = context.BackgroundJobStore;

        Assert.True(store.SupportsJobLease);
        Assert.True(store.SupportsJobManagement);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 领取的作业携带令牌与租约到期时间
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 领取的作业携带令牌与租约到期时间()
    {
        using var context = new TasksTestContext();
        await context.BackgroundJobStore.InsertAsync(NewJob());

        var claimed = Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));

        Assert.False(string.IsNullOrEmpty(claimed.ClaimToken));
        Assert.Equal(Now.AddMinutes(5), claimed.LeaseExpiresAt);
        Assert.False(claimed.IsCancellationRequested);
    }

    /// <summary>
    /// 租约过期后被他人领取，旧令牌的续租、完成与回写均不命中，新令牌完成命中
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 租约过期被他人领取后旧令牌全部不命中()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);

        var first = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(6);
        var second = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.NotEqual(first.ClaimToken, second.ClaimToken);

        var staleLease = ToLease(first);
        Assert.Null(await store.TryRenewLeaseAsync(staleLease, cancellationToken));
        Assert.False(await store.TryCompleteAsync(staleLease, cancellationToken));
        first.TryCount = 1;
        first.NextTryTime = Now.AddMinutes(10);
        Assert.False(await store.TryUpdateAsync(first, staleLease, cancellationToken));

        var stored = await FindEntityAsync(context, job.Id);
        Assert.NotNull(stored);
        Assert.Equal(second.ClaimToken, stored.ClaimToken);
        Assert.Equal((short)0, stored.TryCount);

        Assert.True(await store.TryCompleteAsync(ToLease(second), cancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 续租延长租约，原到期之后仍不能被再次领取
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 续租延长租约()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        await store.InsertAsync(NewJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(4);
        var renewed = await store.TryRenewLeaseAsync(ToLease(claimed), cancellationToken);

        Assert.NotNull(renewed);
        Assert.Equal(claimed.ClaimToken, renewed.Token);
        Assert.Equal(Now.AddMinutes(9), renewed.ExpiresAt);
        Assert.False(renewed.IsCancellationRequested);

        context.Clock.Now = Now.AddMinutes(8);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(10);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 租约已过期时即使未被他人领取也不能续租
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 租约已过期时不能续租()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        await store.InsertAsync(NewJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(6);

        Assert.Null(await store.TryRenewLeaseAsync(ToLease(claimed), cancellationToken));
    }

    /// <summary>
    /// 续租恰在租约到期那一刻仍命中，此刻作业也不能被他人领取
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 续租恰在到期那一刻仍命中且不能被他人领取()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        await store.InsertAsync(NewJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(5);

        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
        var renewed = await store.TryRenewLeaseAsync(ToLease(claimed), cancellationToken);
        Assert.NotNull(renewed);
        Assert.Equal(Now.AddMinutes(10), renewed.ExpiresAt);
    }

    /// <summary>
    /// 租约已过期但未被他人领取时，按令牌完成仍命中
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 租约过期但未被他人领取时按令牌完成仍命中()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(6);

        Assert.True(await store.TryCompleteAsync(ToLease(claimed), cancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 按令牌回写后租约结束，到下次执行时间可再次领取
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 按令牌回写后租约结束且到期可再次领取()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.TryCount = 1;
        claimed.LastTryTime = Now;
        claimed.NextTryTime = Now.AddMinutes(2);
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), cancellationToken));

        var stored = await FindEntityAsync(context, job.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.ClaimTime);
        Assert.Equal((short)1, stored.TryCount);
        Assert.False(stored.IsAbandoned);

        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        context.Clock.Now = Now.AddMinutes(3);
        var again = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.Equal(job.Id, again.Id);
    }

    /// <summary>
    /// 按令牌回写放弃时作业保留并标记放弃
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 按令牌回写放弃时作业保留并标记放弃()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.TryCount = 3;
        claimed.IsAbandoned = true;
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.True(found.IsAbandoned);
        Assert.False(found.IsCancellationRequested);
        Assert.Null(found.ClaimToken);
    }

    /// <summary>
    /// 领取后登记的取消请求不被按令牌回写覆盖，回写按放弃处理
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 领取后登记的取消请求不被回写覆盖()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.Equal(
            BackgroundJobManagementStatus.CancellationRequested,
            await store.RequestCancellationAsync(job.Id, cancellationToken));

        claimed.TryCount = 1;
        claimed.LastTryTime = Now;
        claimed.NextTryTime = Now.AddSeconds(10);

        Assert.False(claimed.IsCancellationRequested);
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.True(found.IsAbandoned);
        Assert.True(found.IsCancellationRequested);
        Assert.Equal((short)1, found.TryCount);
        Assert.Null(found.ClaimToken);

        context.Clock.Now = Now.AddMinutes(1);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 回写携带取消请求时作业按放弃处理
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 回写携带取消请求时按放弃处理()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.IsCancellationRequested = true;
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), cancellationToken));

        var found = await store.FindAsync(job.Id);
        Assert.NotNull(found);
        Assert.True(found.IsAbandoned);
        Assert.True(found.IsCancellationRequested);
    }

    /// <summary>
    /// 释放租约后立即可再次领取，错误令牌释放无效
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 释放租约后立即可再次领取且错误令牌释放无效()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        await store.ReleaseLeaseAsync(
            new BackgroundJobLease(job.Id, "not-the-token", Now.AddMinutes(5)),
            cancellationToken);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        await store.ReleaseLeaseAsync(ToLease(claimed), cancellationToken);

        var again = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.Equal(job.Id, again.Id);
        Assert.NotEqual(claimed.ClaimToken, again.ClaimToken);
    }

    /// <summary>
    /// 旧的整行更新仍可用并结束租约
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 旧的整行更新仍可用并结束租约()
    {
        using var context = new TasksTestContext();
        var store = context.BackgroundJobStore;
        var job = NewJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.TryCount = 2;
        await store.UpdateAsync(claimed);

        var stored = await FindEntityAsync(context, job.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.ClaimToken);
        Assert.Equal((short)2, stored.TryCount);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 不含取消字段的旧表经 SqlSugar CodeFirst.InitTables 补列后旧数据可读且可领取，重复调用 InitTables 不出错
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 旧表经CodeFirst初始化补列后旧数据可读且可领取()
    {
        using var context = new TasksTestContext();
        var client = context.Client;

        client.Ado.ExecuteCommand("DROP TABLE sys_background_job");
        client.Ado.ExecuteCommand("""
            CREATE TABLE sys_background_job (
                Basic_Id TEXT NOT NULL PRIMARY KEY,
                Row_Version INTEGER NOT NULL,
                Application_Name VARCHAR(128) NOT NULL,
                Tenant_Id INTEGER NULL,
                Job_Name VARCHAR(256) NOT NULL,
                Job_Args TEXT NOT NULL,
                Try_Count INTEGER NOT NULL,
                Creation_Time DATETIME NOT NULL,
                Next_Try_Time DATETIME NOT NULL,
                Last_Try_Time DATETIME NULL,
                Is_Abandoned BIT NOT NULL,
                Priority INTEGER NOT NULL,
                Claim_Token VARCHAR(64) NULL,
                Claim_Time DATETIME NULL
            )
            """);
        Assert.DoesNotContain(GetColumnNames(client), name => name == "Is_Cancellation_Requested");

        var legacyId = Guid.NewGuid();
        await client.Insertable(new SysBackgroundJob(legacyId)
            {
                JobName = "Order.Close",
                JobArgs = "{}",
                CreationTime = Now.AddMinutes(-30),
                NextTryTime = Now.AddMinutes(-1)
            })
            .IgnoreColumns(item => item.IsCancellationRequested)
            .ExecuteCommandAsync();

        client.CodeFirst.InitTables(typeof(SysBackgroundJob));
        client.CodeFirst.InitTables(typeof(SysBackgroundJob));

        Assert.Contains(GetColumnNames(client), name => name == "Is_Cancellation_Requested");

        var found = await context.BackgroundJobStore.FindAsync(legacyId);
        Assert.NotNull(found);
        Assert.False(found.IsCancellationRequested);

        var claimed = Assert.Single(await context.BackgroundJobStore.GetWaitingJobsAsync(null, 10));
        Assert.Equal(legacyId, claimed.Id);
        Assert.False(claimed.IsCancellationRequested);
    }

    private static List<string> GetColumnNames(ISqlSugarClient client)
    {
        return [.. client.DbMaintenance.GetColumnInfosByTableName("sys_background_job", false).Select(column => column.DbColumnName)];
    }

    private static Task<SysBackgroundJob> FindEntityAsync(TasksTestContext context, Guid jobId)
    {
        return context.Client.Queryable<SysBackgroundJob>()
            .Where(item => item.BasicId == jobId)
            .FirstAsync();
    }

    private static BackgroundJobLease ToLease(BackgroundJobInfo job)
    {
        Assert.NotNull(job.ClaimToken);
        Assert.NotNull(job.LeaseExpiresAt);
        return new BackgroundJobLease(job.Id, job.ClaimToken, job.LeaseExpiresAt.Value, job.IsCancellationRequested);
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
