// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 进程内后台作业存储的逐作业租约与管理测试
/// </summary>
public class DefaultBackgroundJobStoreLeaseTests
{
    private const int LeaseSeconds = 60;

    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 存储声明支持租约与管理
    /// </summary>
    [Fact]
    public void Store_SupportsLeaseAndManagement()
    {
        var store = CreateStore(new FakeClock(Now));

        Assert.True(store.SupportsJobLease);
        Assert.True(store.SupportsJobManagement);
    }

    /// <summary>
    /// 领取返回带令牌与到期时间的副本；租约有效期内再次领取拿不到
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_ReturnsLeasedCopy_AndHidesLeasedJob()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);

        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        Assert.NotSame(job, claimed);
        Assert.Equal(job.Id, claimed.Id);
        Assert.False(string.IsNullOrEmpty(claimed.ClaimToken));
        Assert.Equal(Now.AddSeconds(LeaseSeconds), claimed.LeaseExpiresAt);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 旧领取者的租约到期后被他人领取：旧令牌续租、完成、回写全部未命中，新令牌完成命中
    /// </summary>
    [Fact]
    public async Task ExpiredLease_ReclaimedByAnother_OldTokenIsFenced()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);

        var a = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        clock.Now = Now.AddSeconds(LeaseSeconds);
        var b = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        var leaseA = ToLease(a);
        var leaseB = ToLease(b);

        Assert.NotEqual(a.ClaimToken, b.ClaimToken);
        Assert.Null(await store.TryRenewLeaseAsync(leaseA, TestContext.Current.CancellationToken));
        Assert.False(await store.TryCompleteAsync(leaseA, TestContext.Current.CancellationToken));
        a.TryCount = 5;
        Assert.False(await store.TryUpdateAsync(a, leaseA, TestContext.Current.CancellationToken));

        var stored = await store.FindAsync(job.Id);
        Assert.NotNull(stored);
        Assert.Equal(b.ClaimToken, stored.ClaimToken);
        Assert.Equal((short)0, stored.TryCount);

        Assert.True(await store.TryCompleteAsync(leaseB, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 续租延长到期时间：原到期之后、新到期之前仍领取不到
    /// </summary>
    [Fact]
    public async Task TryRenewLease_ExtendsExpiry()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        await store.InsertAsync(CreateJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddSeconds(30);
        var renewed = await store.TryRenewLeaseAsync(ToLease(claimed), TestContext.Current.CancellationToken);

        Assert.NotNull(renewed);
        Assert.Equal(claimed.ClaimToken, renewed.Token);
        Assert.Equal(Now.AddSeconds(30 + LeaseSeconds), renewed.ExpiresAt);
        Assert.False(renewed.IsCancellationRequested);

        clock.Now = Now.AddSeconds(LeaseSeconds + 10);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddSeconds(30 + LeaseSeconds);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 已到期的租约即使未被他人领取也续租失败
    /// </summary>
    [Fact]
    public async Task TryRenewLease_WhenExpired_ReturnsNull()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        await store.InsertAsync(CreateJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddSeconds(LeaseSeconds);

        Assert.Null(await store.TryRenewLeaseAsync(ToLease(claimed), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 续租不存在的作业返回 null
    /// </summary>
    [Fact]
    public async Task TryRenewLease_WhenMissing_ReturnsNull()
    {
        var store = CreateStore(new FakeClock(Now));

        var lease = new BackgroundJobLease(Guid.NewGuid(), "token", Now.AddSeconds(LeaseSeconds));

        Assert.Null(await store.TryRenewLeaseAsync(lease, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 按令牌回写命中后租约结束，到下次执行时间可再被领取
    /// </summary>
    [Fact]
    public async Task TryUpdate_WhenTokenMatches_EndsLeaseAndWritesFields()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.TryCount = 1;
        claimed.LastTryTime = Now;
        claimed.NextTryTime = Now.AddSeconds(10);
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), TestContext.Current.CancellationToken));

        var stored = await store.FindAsync(job.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.LeaseExpiresAt);
        Assert.Equal((short)1, stored.TryCount);
        Assert.Equal(Now, stored.LastTryTime);
        Assert.Equal(Now.AddSeconds(10), stored.NextTryTime);

        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));
        clock.Now = Now.AddSeconds(10);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 按令牌回写放弃状态时移除作业
    /// </summary>
    [Fact]
    public async Task TryUpdate_WhenAbandoned_RemovesJob()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        claimed.IsAbandoned = true;

        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 按令牌回写不覆盖回写前登记的取消请求：未放弃的回写转为取消并移除作业
    /// </summary>
    [Fact]
    public async Task TryUpdate_WhenCancellationRequestedAfterClaim_CancelsAndRemoves()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));
        Assert.Equal(
            BackgroundJobManagementStatus.CancellationRequested,
            await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken));

        claimed.TryCount = 1;
        claimed.NextTryTime = Now.AddSeconds(10);

        Assert.False(claimed.IsCancellationRequested);
        Assert.True(await store.TryUpdateAsync(claimed, ToLease(claimed), TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 租约已到期但未被他人领取时，按令牌完成仍命中
    /// </summary>
    [Fact]
    public async Task TryComplete_WhenLeaseExpiredButNotReclaimed_ReturnsTrue()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddSeconds(LeaseSeconds + 1);

        Assert.True(await store.TryCompleteAsync(ToLease(claimed), TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 租约已到期的作业视为未在执行：取消直接生效并移除
    /// </summary>
    [Fact]
    public async Task RequestCancellation_WhenLeaseExpired_CancelsAndRemoves()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddSeconds(LeaseSeconds);

        Assert.Equal(
            BackgroundJobManagementStatus.Cancelled,
            await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 释放租约后立即可再领取；错误令牌释放无效
    /// </summary>
    [Fact]
    public async Task ReleaseLease_MakesJobClaimableAgain_OnlyWithMatchingToken()
    {
        var store = CreateStore(new FakeClock(Now));
        await store.InsertAsync(CreateJob());
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        await store.ReleaseLeaseAsync(ToLease(claimed) with { Token = "wrong" }, TestContext.Current.CancellationToken);
        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        await store.ReleaseLeaseAsync(ToLease(claimed), TestContext.Current.CancellationToken);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 旧的无令牌回写同样结束租约
    /// </summary>
    [Fact]
    public async Task Update_ClearsLease()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        await store.UpdateAsync(claimed);

        var stored = await store.FindAsync(job.Id);
        Assert.NotNull(stored);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.LeaseExpiresAt);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 取消等待中的作业：直接取消并移除
    /// </summary>
    [Fact]
    public async Task RequestCancellation_WhenWaiting_CancelsAndRemoves()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);

        var status = await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Cancelled, status);
        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 取消持租中的作业：登记取消请求，续租可见；重复请求不再变更
    /// </summary>
    [Fact]
    public async Task RequestCancellation_WhenLeased_RecordsRequest()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);
        var claimed = Assert.Single(await store.GetWaitingJobsAsync(null, 10));

        var first = await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);
        var second = await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken);
        var renewed = await store.TryRenewLeaseAsync(ToLease(claimed), TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, first);
        Assert.Equal(BackgroundJobManagementStatus.NoChange, second);
        Assert.NotNull(renewed);
        Assert.True(renewed.IsCancellationRequested);
        Assert.NotNull(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 取消不存在的作业返回 NotFound
    /// </summary>
    [Fact]
    public async Task RequestCancellation_WhenMissing_ReturnsNotFound()
    {
        var store = CreateStore(new FakeClock(Now));

        Assert.Equal(
            BackgroundJobManagementStatus.NotFound,
            await store.RequestCancellationAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 取消已放弃的作业不做变更
    /// </summary>
    [Fact]
    public async Task RequestCancellation_WhenAbandoned_ReturnsNoChange()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        job.IsAbandoned = true;
        await store.InsertAsync(job);

        Assert.Equal(
            BackgroundJobManagementStatus.NoChange,
            await store.RequestCancellationAsync(job.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 重试：未放弃返回 NoChange，不存在返回 NotFound
    /// </summary>
    [Fact]
    public async Task RetryAbandoned_WhenNotAbandonedOrMissing_ReturnsNoChangeOrNotFound()
    {
        var store = CreateStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);

        Assert.Equal(
            BackgroundJobManagementStatus.NoChange,
            await store.RetryAbandonedAsync(job.Id, TestContext.Current.CancellationToken));
        Assert.Equal(
            BackgroundJobManagementStatus.NotFound,
            await store.RetryAbandonedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 重试已放弃的作业：重新排入待执行，尝试次数归零，再次重试不再变更
    /// </summary>
    [Fact]
    public async Task RetryAbandoned_WhenAbandoned_Reschedules()
    {
        var clock = new FakeClock(Now);
        var store = CreateStore(clock);
        var job = CreateJob();
        await store.InsertAsync(job);
        var stored = await store.FindAsync(job.Id);
        stored!.IsAbandoned = true;
        stored.IsCancellationRequested = true;
        stored.TryCount = 7;
        stored.NextTryTime = Now.AddDays(1);
        clock.Now = Now.AddMinutes(5);

        var first = await store.RetryAbandonedAsync(job.Id, TestContext.Current.CancellationToken);
        var second = await store.RetryAbandonedAsync(job.Id, TestContext.Current.CancellationToken);

        Assert.Equal(BackgroundJobManagementStatus.Rescheduled, first);
        Assert.Equal(BackgroundJobManagementStatus.NoChange, second);
        var rescheduled = await store.FindAsync(job.Id);
        Assert.NotNull(rescheduled);
        Assert.False(rescheduled.IsAbandoned);
        Assert.False(rescheduled.IsCancellationRequested);
        Assert.Equal((short)0, rescheduled.TryCount);
        Assert.Equal(Now.AddMinutes(5), rescheduled.NextTryTime);
        Assert.Null(rescheduled.ClaimToken);
        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 租约时长不大于 0 时构造抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenLeaseDurationNotPositive_Throws()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new BackgroundJobWorkerOptions { JobLeaseDurationSeconds = 0 });

        Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultBackgroundJobStore(new FakeClock(Now), options));
    }

    /// <summary>
    /// 创建使用测试租约时长的存储
    /// </summary>
    /// <param name="clock">时钟</param>
    /// <returns>存储</returns>
    private static DefaultBackgroundJobStore CreateStore(FakeClock clock)
    {
        return new DefaultBackgroundJobStore(
            clock,
            Microsoft.Extensions.Options.Options.Create(new BackgroundJobWorkerOptions { JobLeaseDurationSeconds = LeaseSeconds }));
    }

    /// <summary>
    /// 由领取结果构造租约
    /// </summary>
    /// <param name="job">领取到的作业</param>
    /// <returns>租约</returns>
    private static BackgroundJobLease ToLease(BackgroundJobInfo job)
    {
        return new BackgroundJobLease(job.Id, job.ClaimToken!, job.LeaseExpiresAt!.Value);
    }

    /// <summary>
    /// 构造一条可立即执行的作业记录
    /// </summary>
    /// <returns>作业记录</returns>
    private static BackgroundJobInfo CreateJob()
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "job",
            JobArgs = "{}",
            CreationTime = Now,
            NextTryTime = Now
        };
    }
}
