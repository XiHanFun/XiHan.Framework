// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 进程内内存后台作业存储测试
/// </summary>
/// <remarks>
/// 领取待执行作业的过滤 + 排序 + 限量三段语义写在接口注释里，是所有存储实现共用的契约，
/// 内存实现是这份契约的参考实现，因此逐条覆盖：应用名按序数比较、放弃的不取、
/// 未到时间的不取、优先级降序 → 尝试次数升序 → 下次执行时间升序、超出上限截断。
/// </remarks>
public class InMemoryBackgroundJobStoreTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 插入后可按标识查回同一实例
    /// </summary>
    [Fact]
    public async Task InsertThenFind_ReturnsSameInstance()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var job = CreateJob();

        await store.InsertAsync(job);
        var found = await store.FindAsync(job.Id);

        Assert.Same(job, found);
    }

    /// <summary>
    /// 查询不存在的作业返回 null
    /// </summary>
    [Fact]
    public async Task Find_WhenMissing_ReturnsNull()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));

        Assert.Null(await store.FindAsync(Guid.NewGuid()));
    }

    /// <summary>
    /// 插入 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task Insert_WhenJobNull_ThrowsArgumentNullException()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.InsertAsync(null!));
    }

    /// <summary>
    /// 更新 null 时抛出空引用参数异常
    /// </summary>
    [Fact]
    public async Task Update_WhenJobNull_ThrowsArgumentNullException()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpdateAsync(null!));
    }

    /// <summary>
    /// 删除后不再能查到，且重复删除不报错
    /// </summary>
    [Fact]
    public async Task Delete_RemovesJobAndIsIdempotent()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);

        await store.DeleteAsync(job.Id);
        await store.DeleteAsync(job.Id);

        Assert.Null(await store.FindAsync(job.Id));
    }

    /// <summary>
    /// 更新会覆盖同标识的记录
    /// </summary>
    [Fact]
    public async Task Update_ReplacesStoredJob()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var job = CreateJob();
        await store.InsertAsync(job);

        var replacement = CreateJob();
        replacement.Id = job.Id;
        replacement.JobName = "renamed";
        await store.UpdateAsync(replacement);

        var found = await store.FindAsync(job.Id);

        Assert.Same(replacement, found);
        Assert.Equal("renamed", found!.JobName);
    }

    /// <summary>
    /// 应用名按序数比较：大小写不同视为不同实例，互不串扰
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_MatchesApplicationNameOrdinally()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        await store.InsertAsync(CreateJob(applicationName: "OrderService"));
        await store.InsertAsync(CreateJob(applicationName: "orderservice"));
        await store.InsertAsync(CreateJob(applicationName: null));

        var exact = await store.GetWaitingJobsAsync("OrderService", 10);
        var hostLevel = await store.GetWaitingJobsAsync(null, 10);

        Assert.Single(exact);
        Assert.Equal("OrderService", exact[0].ApplicationName);
        Assert.Single(hostLevel);
        Assert.Null(hostLevel[0].ApplicationName);
    }

    /// <summary>
    /// 已放弃的作业不再被领取
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_SkipsAbandonedJobs()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var abandoned = CreateJob();
        abandoned.IsAbandoned = true;
        await store.InsertAsync(abandoned);
        await store.InsertAsync(CreateJob());

        var waiting = await store.GetWaitingJobsAsync(null, 10);

        Assert.Single(waiting);
        Assert.False(waiting[0].IsAbandoned);
    }

    /// <summary>
    /// 下次执行时间还没到的作业不被领取，时钟推进后才可见
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_SkipsJobsScheduledInFuture()
    {
        var clock = new FakeClock(Now);
        var store = new InMemoryBackgroundJobStore(clock);
        var delayed = CreateJob();
        delayed.NextTryTime = Now.AddMinutes(5);
        await store.InsertAsync(delayed);

        Assert.Empty(await store.GetWaitingJobsAsync(null, 10));

        clock.Now = Now.AddMinutes(5);

        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 恰好等于当前时间的作业可被领取（边界包含）
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_IncludesJobDueExactlyNow()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var job = CreateJob();
        job.NextTryTime = Now;
        await store.InsertAsync(job);

        Assert.Single(await store.GetWaitingJobsAsync(null, 10));
    }

    /// <summary>
    /// 排序契约：优先级降序 → 尝试次数升序 → 下次执行时间升序
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_OrdersByPriorityThenTryCountThenNextTryTime()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));

        var lowPriority = CreateJob(jobName: "low");
        lowPriority.Priority = BackgroundJobPriority.Low;

        var highRetriedLater = CreateJob(jobName: "high-retried");
        highRetriedLater.Priority = BackgroundJobPriority.High;
        highRetriedLater.TryCount = 2;
        highRetriedLater.NextTryTime = Now.AddMinutes(-10);

        var highFreshLate = CreateJob(jobName: "high-fresh-late");
        highFreshLate.Priority = BackgroundJobPriority.High;
        highFreshLate.TryCount = 0;
        highFreshLate.NextTryTime = Now.AddMinutes(-1);

        var highFreshEarly = CreateJob(jobName: "high-fresh-early");
        highFreshEarly.Priority = BackgroundJobPriority.High;
        highFreshEarly.TryCount = 0;
        highFreshEarly.NextTryTime = Now.AddMinutes(-30);

        await store.InsertAsync(lowPriority);
        await store.InsertAsync(highRetriedLater);
        await store.InsertAsync(highFreshLate);
        await store.InsertAsync(highFreshEarly);

        var waiting = await store.GetWaitingJobsAsync(null, 10);

        Assert.Equal(
            new[] { "high-fresh-early", "high-fresh-late", "high-retried", "low" },
            waiting.Select(x => x.JobName).ToArray());
    }

    /// <summary>
    /// 超过上限时截断，且截断的是排序后的尾部
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_TakesAtMostMaxResultCount()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));

        var high = CreateJob(jobName: "high");
        high.Priority = BackgroundJobPriority.High;
        var normal = CreateJob(jobName: "normal");
        var low = CreateJob(jobName: "low");
        low.Priority = BackgroundJobPriority.Low;

        await store.InsertAsync(low);
        await store.InsertAsync(normal);
        await store.InsertAsync(high);

        var waiting = await store.GetWaitingJobsAsync(null, 2);

        Assert.Equal(new[] { "high", "normal" }, waiting.Select(x => x.JobName).ToArray());
    }

    /// <summary>
    /// 上限为 0 时返回空列表
    /// </summary>
    [Fact]
    public async Task GetWaitingJobs_WhenMaxResultCountIsZero_ReturnsEmpty()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        await store.InsertAsync(CreateJob());

        Assert.Empty(await store.GetWaitingJobsAsync(null, 0));
    }

    /// <summary>
    /// 并发插入不同作业不丢数据（底层为并发字典，宣称可多线程使用）
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task InsertAsync_UnderConcurrency_KeepsAllJobs()
    {
        var store = new InMemoryBackgroundJobStore(new FakeClock(Now));
        var jobs = Enumerable.Range(0, 200).Select(_ => CreateJob()).ToArray();

        await Parallel.ForEachAsync(
            jobs,
            TestContext.Current.CancellationToken,
            async (job, _) => await store.InsertAsync(job));

        var waiting = await store.GetWaitingJobsAsync(null, jobs.Length);

        Assert.Equal(jobs.Length, waiting.Count);
    }

    /// <summary>
    /// 构造一条默认可立即执行的作业记录
    /// </summary>
    /// <param name="jobName">作业名</param>
    /// <param name="applicationName">应用名</param>
    /// <returns>作业记录</returns>
    private static BackgroundJobInfo CreateJob(string jobName = "job", string? applicationName = null)
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            ApplicationName = applicationName,
            JobName = jobName,
            JobArgs = "{}",
            CreationTime = Now,
            NextTryTime = Now
        };
    }
}
