// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 内存实现自己的行为：假时钟上的 cron、不共享存储下的领导语义、历史上限、选项。
/// </summary>
public sealed class InMemorySchedulingTests
{
    /// <summary>
    /// 顶替领导选举的显式 Provider：永远拿不到租约。
    /// </summary>
    private sealed class ExplicitLeaderFeature : IFeature
    {
        /// <inheritdoc />
        public Manifest Manifest { get; } = TestManifests.Create("xihan.scheduling.fake-leader", provides: ["scheduling.leader@1"], requires: ["kernel.hosting@1"]);

        /// <inheritdoc />
        public void Configure(FeatureContext context) => context.Services.AddSingleton<ILeaderElection>(new NeverLeaderElection());
    }

    /// <summary>
    /// 永远拒绝的选举。
    /// </summary>
    private sealed class NeverLeaderElection : ILeaderElection
    {
        /// <inheritdoc />
        public ValueTask<Result<LeaderLease>> TryAcquireAsync(JobName resource, TimeSpan timeToLive, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<LeaderLease>>(Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", resource.Value))));
    }

    [Fact]
    public async Task CronJob_RunsWhenDue_OnTheFakeClock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 23, 2, 59, 30, TimeSpan.Zero));
        var log = new RecordingJobLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>(),
            [new("XiHan:Scheduling:PollIntervalSeconds", "1")],
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton(log);
                services.AddJob<RecordingJob>("nightly", JobSchedule.FromCron("0 3 * * *"));
            });
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        // 第一个刻度：同步代码作业、取得租约，作业未到期
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => engine.IsLeader);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero), (await StatusAsync(engine, "nightly")).NextRun);
        Assert.Empty(log.Runs);
        // 跨过 03:00 的刻度：作业运行，下一次是明天（从计划时刻推算，节奏不漂移）
        clock.Advance(TimeSpan.FromSeconds(30));
        await log.WaitAsync("nightly", 1, TimeSpan.FromSeconds(10));
        Assert.Equal((JobTrigger.Schedule, new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero)), (log.Runs[0].Run.Trigger, log.Runs[0].Run.ScheduledAt));
        await WaitUntilAsync(() => !StatusAsync(engine, "nightly").GetAwaiter().GetResult().IsRunning);
        var status = await StatusAsync(engine, "nightly");
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), status.NextRun);
        Assert.Equal((1L, new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero)), (status.Triggers[0].FireCount, status.Triggers[0].PreviousFireAt));
        Assert.Equal(JobSource.Code, status.Job.Source);
    }

    [Fact]
    public async Task ClusterWideJobs_NeedTheLease_OnANonSharedStore_ButLocalAndManualDoNot()
    {
        var log = new RecordingJobLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>().UseFeature<ExplicitLeaderFeature>(),
            [new("XiHan:Scheduling:PollIntervalSeconds", "0.05")],
            services =>
            {
                services.AddSingleton(log);
                services.AddJob<RecordingJob>("tick", JobSchedule.Every(TimeSpan.FromMilliseconds(50)));
                services.AddJob<RecordingJob>(new JobDefinition("free", JobSchedule.Every(TimeSpan.FromMilliseconds(50)), typeof(RecordingJob)) { RunOnEveryInstance = true });
            });
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        // 显式 Provider 赢得 scheduling.leader：本包不再绑定自己的选举，引擎拿不到租约
        Assert.IsType<NeverLeaderElection>(host.Services.GetRequiredService<ILeaderElection>());
        Assert.Equal(["scheduling.jobs"], host.App.Graph["xihan.scheduling.inmemory"].SelectedCapabilities);
        Assert.False(host.Services.GetRequiredService<IJobStore>().IsShared);
        await log.WaitAsync("free", 2, TimeSpan.FromSeconds(10));
        Assert.False(engine.IsLeader);
        Assert.DoesNotContain(log.Runs, static r => r.Run.Name == new JobName("tick"));
        Assert.True((await engine.TriggerAsync("tick", CancellationToken.None)).IsSuccess);
        Assert.Single(log.Runs, static r => r.Run.Name == new JobName("tick") && r.Run.Trigger == JobTrigger.Manual);
    }

    [Fact]
    public async Task RunHistory_IsBounded_EvictingOldestFinished()
    {
        var store = new InMemoryJobStore(new InMemorySchedulingOptions { MaxRuns = 2 });
        var job = new JobRecord { Name = "cap", JobTypeName = "t" };
        await store.SaveJobsAsync([job], CancellationToken.None);
        var runs = Enumerable.Range(0, 3).Select(index => new JobRunRecord { Id = $"r{index}", Job = "cap", NodeId = "n", StartedAt = DateTimeOffset.UtcNow.AddSeconds(index), ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(index) }).ToList();
        Assert.True((await store.TryBeginRunAsync(runs[0], false, CancellationToken.None)).ValueOrThrow());
        Assert.True((await store.TryBeginRunAsync(runs[1], false, CancellationToken.None)).ValueOrThrow());
        // 满了且都在运行中：无法淘汰
        Assert.Equal(SchedulingErrors.StoreCapacity, (await store.TryBeginRunAsync(runs[2], false, CancellationToken.None)).Error?.Code);
        runs[0].State = JobRunState.Succeeded;
        runs[0].CompletedAt = DateTimeOffset.UtcNow;
        Assert.True((await store.CompleteRunAsync(runs[0], CancellationToken.None)).IsSuccess);
        Assert.True((await store.TryBeginRunAsync(runs[2], false, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(2, store.RunCount);
        Assert.Equal(["r2", "r1"], (await store.RunsAsync(new JobRunQuery(), CancellationToken.None)).ValueOrThrow().Select(static r => r.Id));
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemorySchedulingOptions { MaxRuns = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new SchedulerOptions { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new SchedulerOptions { LeaseTimeToLive = TimeSpan.FromSeconds(-1) });
        Assert.Throws<ArgumentException>(static () => new SchedulerOptions { NodeId = " " });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Scheduling:PollIntervalSeconds"] = "0.5",
            ["XiHan:Scheduling:LeaseResource"] = "jobs.primary",
            ["XiHan:Scheduling:LeaseTimeToLiveSeconds"] = "15",
            ["XiHan:Scheduling:NodeId"] = "node-1",
            ["XiHan:Scheduling:MaxConcurrentRuns"] = "4",
            ["XiHan:Scheduling:MisfireThresholdSeconds"] = "5",
            ["XiHan:Scheduling:HistoryRetentionDays"] = "0",
            ["XiHan:Scheduling:InMemory:MaxRuns"] = "7",
        }).Build();
        var options = SchedulerOptions.FromConfiguration(configuration);
        Assert.Equal((TimeSpan.FromMilliseconds(500), new JobName("jobs.primary"), TimeSpan.FromSeconds(15), "node-1", 4, TimeSpan.FromSeconds(5), TimeSpan.Zero), (options.PollInterval, options.LeaseResource, options.LeaseTimeToLive, options.NodeId, options.MaxConcurrentRuns, options.MisfireThreshold, options.HistoryRetention));
        Assert.Equal(7, InMemorySchedulingOptions.FromConfiguration(configuration).MaxRuns);
        Assert.Throws<ArgumentException>(static () => SchedulerOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Scheduling:PollIntervalSeconds"] = "soon" }).Build()));
        Assert.Throws<ArgumentException>(static () => InMemorySchedulingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Scheduling:InMemory:MaxRuns"] = "-1" }).Build()));
    }

    /// <summary>
    /// 作业状态。
    /// </summary>
    private static async Task<JobStatus> StatusAsync(JobSchedulerEngine engine, JobName name)
        => (await engine.StatusAsync(name, CancellationToken.None)).ValueOrThrow().ValueOrThrow();

    /// <summary>
    /// 等待条件成立。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
