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
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 内存实现特有的行为：cron 作业按假时钟到期运行、领导租约决定按计划的运行、选项解析、让位给显式选举 Provider。
/// </summary>
public sealed class InMemorySchedulingTests
{
    /// <summary>
    /// 模拟一个显式的领导选举 Provider，只声明能力，不注册服务。
    /// </summary>
    private sealed class ExplicitLeaderFeature : IFeature
    {
        /// <summary>
        /// 以 Provider 角色声明 <c>scheduling.leader@1</c> 的清单。
        /// </summary>
        public Manifest Manifest { get; } = TestManifests.Create("xihan.scheduling.fake-leader", provides: ["scheduling.leader@1"], requires: ["kernel.hosting@1"]);

        /// <summary>
        /// 注册一个永远拿不到租约的选举，验证调度器确实用了它。
        /// </summary>
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
            [new("XiHan:Scheduling:InMemory:PollIntervalSeconds", "1")],
            services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton(log);
                services.AddJob<RecordingJob>("nightly", JobSchedule.FromCron("0 3 * * *"));
            });
        var scheduler = host.Services.GetRequiredService<InMemoryJobScheduler>();

        Assert.Equal(new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero), scheduler.Status("nightly").ValueOrThrow().NextRun);
        // 第一个刻度：取得租约，作业未到期
        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => scheduler.IsLeader);
        Assert.Empty(log.Runs);

        // 跨过 03:00 的刻度：作业运行，下一次是明天
        clock.Advance(TimeSpan.FromSeconds(30));
        await log.WaitAsync("nightly", 1, TimeSpan.FromSeconds(10));
        Assert.Equal(JobTrigger.Schedule, log.Runs[0].Run.Trigger);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero), log.Runs[0].Run.ScheduledAt);
        await WaitUntilAsync(() => !scheduler.Status("nightly").ValueOrThrow().IsRunning);
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), scheduler.Status("nightly").ValueOrThrow().NextRun);
    }

    [Fact]
    public async Task LeaderOnlyJobs_DoNotRunWithoutTheLease_ButManualTriggerDoes()
    {
        var log = new RecordingJobLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>().UseFeature<ExplicitLeaderFeature>(),
            [new("XiHan:Scheduling:InMemory:PollIntervalSeconds", "0.05")],
            services =>
            {
                services.AddSingleton(log);
                services.AddJob<RecordingJob>("tick", JobSchedule.Every(TimeSpan.FromMilliseconds(50)));
                services.AddJob<RecordingJob>(new JobDefinition("free", JobSchedule.Every(TimeSpan.FromMilliseconds(50)), typeof(RecordingJob)) { LeaderOnly = false });
            });
        var scheduler = host.Services.GetRequiredService<InMemoryJobScheduler>();

        // 显式 Provider 赢得 scheduling.leader：本包不再绑定自己的选举，调度器拿不到租约
        Assert.IsType<NeverLeaderElection>(host.Services.GetRequiredService<ILeaderElection>());
        Assert.Equal(["scheduling.jobs"], host.App.Graph["xihan.scheduling.inmemory"].SelectedCapabilities);
        await log.WaitAsync("free", 2, TimeSpan.FromSeconds(10));
        Assert.False(scheduler.IsLeader);
        Assert.DoesNotContain(log.Runs, static r => r.Run.Name == new JobName("tick"));

        Assert.True((await scheduler.TriggerAsync("tick", CancellationToken.None)).IsSuccess);
        Assert.Single(log.Runs, static r => r.Run.Name == new JobName("tick") && r.Run.Trigger == JobTrigger.Manual);
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemorySchedulingOptions { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new InMemorySchedulingOptions { LeaseTimeToLive = TimeSpan.FromSeconds(-1) });

        var options = InMemorySchedulingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Scheduling:InMemory:PollIntervalSeconds"] = "0.5",
            ["XiHan:Scheduling:InMemory:LeaseResource"] = "jobs.primary",
            ["XiHan:Scheduling:InMemory:LeaseTimeToLiveSeconds"] = "15",
        }).Build());
        Assert.Equal((TimeSpan.FromMilliseconds(500), new JobName("jobs.primary"), TimeSpan.FromSeconds(15)), (options.PollInterval, options.LeaseResource, options.LeaseTimeToLive));
        Assert.Throws<ArgumentException>(static () => InMemorySchedulingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Scheduling:InMemory:PollIntervalSeconds"] = "soon" }).Build()));
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
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
