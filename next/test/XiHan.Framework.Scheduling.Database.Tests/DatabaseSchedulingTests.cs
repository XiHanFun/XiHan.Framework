// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Scheduling.Database.Leadership;
using XiHan.Framework.Scheduling.Database.Storage;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 数据库实现自己的行为：特性清单与装配、同刻运行的排序、两个节点共用一个库时独占作业不重叠、领导者恢复失联节点、宿主停止后的不可达。
/// </summary>
public sealed class DatabaseSchedulingTests
{
    /// <summary>
    /// 本文件用例里的作业名。
    /// </summary>
    private static readonly JobName _exclusive = new("db.exclusive");

    [Fact]
    public async Task Feature_IsExplicitProvider_AndWiresStoreAndElection()
    {
        var feature = new DatabaseSchedulingFeature();
        Assert.Equal(("xihan.scheduling.database", ManifestRole.Provider, ManifestKind.Provider), (feature.Manifest.Id, feature.Manifest.Role, feature.Manifest.Kind));
        Assert.Equal([SchedulingCapabilities.Jobs, SchedulingCapabilities.Leader], feature.Manifest.Provides.Select(static capability => capability.Id));
        Assert.Contains(feature.Manifest.Requires, static requirement => requirement.Id == "persistence.relational");
        Assert.Equal(["scheduling.database.leader"], feature.Manifest.Contributes.Health);

        // 对任何 persistence.relational@1 都能装配：这里用内存持久化
        var log = new RecordingJobLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<DatabaseSchedulingFeature>(),
            [new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", "0.05")],
            services =>
            {
                services.AddSingleton(log);
                services.AddJob<RecordingJob>(new JobDefinition(new JobName("db.manual"), JobSchedule.Manual, typeof(RecordingJob)));
            });
        Assert.Equal([SchedulingCapabilities.Jobs, SchedulingCapabilities.Leader], host.App.Graph["xihan.scheduling.database"].SelectedCapabilities.Order(StringComparer.Ordinal));
        Assert.Same(host.Services.GetRequiredService<DatabaseJobStore>(), host.Services.GetRequiredService<IJobStore>());
        Assert.Same(host.Services.GetRequiredService<DatabaseLeaderElection>(), host.Services.GetRequiredService<ILeaderElection>());
        Assert.True(host.Services.GetRequiredService<IJobStore>().IsShared);
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();
        Assert.True(engine.IsRunning);
        Assert.Equal(engine.NodeId, host.Services.GetRequiredService<DatabaseLeaderElection>().Holder);
        await WaitUntilAsync(() => ValueTask.FromResult(engine.IsLeader));
        Assert.True((await engine.TriggerAsync(new JobName("db.manual"), CancellationToken.None)).IsSuccess);
        Assert.Single(log.Runs);
    }

    [Fact]
    public async Task Runs_AtTheSameScheduledTime_ComeBackNewestFirst()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var store = host.Services.GetRequiredService<IJobStore>();
        var job = new JobName("db.ties");
        var now = DateTimeOffset.UtcNow;
        Assert.True((await store.SaveJobsAsync([new JobRecord { Name = job, JobTypeName = "t", AllowConcurrent = true }], CancellationToken.None)).IsSuccess);
        var runs = new List<JobRunRecord>();
        for (var index = 0; index < 3; index++)
        {
            var run = new JobRunRecord { Id = JobRun.NewId(), Job = job, NodeId = "n", ScheduledAt = now, StartedAt = now, State = JobRunState.Running };
            Assert.True((await store.TryBeginRunAsync(run, false, CancellationToken.None)).ValueOrThrow());
            runs.Add(run);
        }

        // 同一计划时刻按写入序号降序：后写入的在前
        var expected = runs.Select(static run => run.Id).Reverse().ToList();
        Assert.Equal(expected, (await store.RunsAsync(new JobRunQuery { Job = job }, CancellationToken.None)).ValueOrThrow().Select(static run => run.Id));

        // 整体替换保留写入序号：倒着完成它们，顺序不变
        foreach (var run in Enumerable.Reverse(runs))
        {
            run.State = JobRunState.Succeeded;
            run.CompletedAt = now.AddSeconds(1);
            Assert.True((await store.CompleteRunAsync(run, CancellationToken.None)).IsSuccess);
        }

        Assert.Equal(expected, (await store.RunsAsync(new JobRunQuery { Job = job }, CancellationToken.None)).ValueOrThrow().Select(static run => run.Id));
        Assert.Equal(runs[^1].Id, Assert.Single((await store.LastRunsAsync([job], CancellationToken.None)).ValueOrThrow()).Id);
    }

    [Fact]
    public async Task TwoNodes_ShareOneDatabase_AndNeverOverlapAnExclusiveJob()
    {
        await using var database = new SqliteDatabase();
        var log = new OverlapLog();
        var first = await database.StartHostAsync(NodeSettings("node-a", "0.05"), services => Register(services, log));
        var second = await database.StartHostAsync(NodeSettings("node-b", "0.07"), services => Register(services, log));
        var engines = (First: first.Services.GetRequiredService<JobSchedulerEngine>(), Second: second.Services.GetRequiredService<JobSchedulerEngine>());

        // 两个节点都写过心跳后再从 node-a 登记作业：作业只写一次，node-b 虽未登记它也能运行（类型两边都登记了）
        var store = first.Services.GetRequiredService<IJobStore>();
        await WaitUntilAsync(async () => (await store.NodesAsync(CancellationToken.None)).ValueOrThrow().Count == 2);
        var definition = new JobDefinition(_exclusive, JobSchedule.Every(TimeSpan.FromMilliseconds(100)), typeof(OverlapProbeJob));
        Assert.True((await first.Services.GetRequiredService<IJobScheduler>().ScheduleAsync(definition, CancellationToken.None)).IsSuccess);

        // 两个节点轮流拿到触发器（轮询周期不同，相位会错开），合计运行次数增长
        await WaitUntilAsync(() => ValueTask.FromResult(log.Runs.Count >= 6 && log.Runs.Select(static run => run.NodeId).Distinct(StringComparer.Ordinal).Count() == 2));
        Assert.True(engines.First.IsLeader ^ engines.Second.IsLeader, "Exactly one node holds the leader lease.");

        // 不允许并发：任何两次运行的区间都不交叠
        var runs = log.Runs.OrderBy(static run => run.StartedAt).ToList();
        for (var index = 1; index < runs.Count; index++)
        {
            Assert.True(runs[index].StartedAt >= runs[index - 1].CompletedAt, $"Run '{runs[index].RunId}' on '{runs[index].NodeId}' started before run '{runs[index - 1].RunId}' on '{runs[index - 1].NodeId}' completed.");
        }

        // 两个节点看到的是同一份历史
        var history = (await first.Services.GetRequiredService<IJobScheduler>().RunsAsync(new JobRunQuery { Job = _exclusive }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["node-a", "node-b"], history.Select(static run => run.NodeId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.All(history.Where(static run => run.State != JobRunState.Running), static run => Assert.Equal(JobRunState.Succeeded, run.State));
    }

    [Fact]
    public async Task LostNode_IsRecovered_ByTheLeader()
    {
        await using var database = new SqliteDatabase();
        var log = new RecordingJobLog();
        var job = new JobName("db.recover");
        var host = await database.StartHostAsync(
            [.. NodeSettings("node-live", "0.05"), new($"{SchedulerOptions.ConfigurationSection}:NodeTimeoutSeconds", "0.1")],
            services =>
            {
                services.AddSingleton(log);
                services.AddJob<RecordingJob>(new JobDefinition(job, JobSchedule.Manual, typeof(RecordingJob)) { RequestsRecovery = true });
            });
        var store = host.Services.GetRequiredService<IJobStore>();
        await WaitUntilAsync(async () => (await store.FindJobAsync(job, CancellationToken.None)).ValueOrThrow().HasValue);

        // 伪造一个很久没心跳的节点：它抢占了一条触发器、独占运行着一次运行
        var now = DateTimeOffset.UtcNow;
        Assert.True((await store.HeartbeatAsync(new SchedulerNodeRecord { Id = "node-ghost", StartedAt = now.AddHours(-1), LastHeartbeatAt = now.AddHours(-1) }, CancellationToken.None)).IsSuccess);
        var trigger = new TriggerRecord { Job = job, Name = "ghost", Schedule = JobSchedule.Every(TimeSpan.FromHours(1)), State = TriggerState.Acquired, AcquiredBy = "node-ghost", AcquiredAt = now, NextFireAt = now.AddHours(1) };
        Assert.True((await store.SaveTriggersAsync([trigger], CancellationToken.None)).IsSuccess);
        var ghostRun = new JobRunRecord { Id = JobRun.NewId(), Job = job, NodeId = "node-ghost", TriggerName = "ghost", ScheduledAt = now, StartedAt = now, State = JobRunState.Running, Data = new JsonObject { ["who"] = "ghost" } };
        Assert.True((await store.TryBeginRunAsync(ghostRun, true, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(ghostRun.Id, (await store.FindJobAsync(job, CancellationToken.None)).ValueOrThrow().ValueOrThrow().ActiveRunId);

        // 领导者在下个维护周期发现失联：运行记为 Lost、占用释放、触发器回到等待、节点删除；要求恢复的作业以恢复触发器重新运行一次
        await log.WaitAsync(job, 1, TimeSpan.FromSeconds(10));
        var (run, _, _) = Assert.Single(log.Runs);
        Assert.Equal((JobTrigger.Recovery, "ghost"), (run.Trigger, run.Data["who"]!.GetValue<string>()));
        Assert.Null(run.TriggerName);
        await WaitUntilAsync(async () => (await store.RunningAsync(job, CancellationToken.None)).ValueOrThrow().Count == 0);
        var lost = Assert.Single((await store.RunsAsync(new JobRunQuery { Job = job, States = [JobRunState.Lost] }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(ghostRun.Id, lost.Id);
        Assert.NotNull(lost.CompletedAt);
        var recovered = Assert.Single((await store.TriggersAsync(job, CancellationToken.None)).ValueOrThrow(), static candidate => candidate.Name == "ghost");
        Assert.Equal((TriggerState.Waiting, now.AddHours(1)), (recovered.State, recovered.NextFireAt));
        Assert.Null(recovered.AcquiredBy);
        Assert.Null((await store.FindJobAsync(job, CancellationToken.None)).ValueOrThrow().ValueOrThrow().ActiveRunId);
        Assert.Equal(["node-live"], (await store.NodesAsync(CancellationToken.None)).ValueOrThrow().Select(static node => node.Id));
        var history = (await store.RunsAsync(new JobRunQuery { Job = job }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, static candidate => candidate.Trigger == JobTrigger.Recovery && candidate.State == JobRunState.Succeeded && candidate.NodeId == "node-live");
    }

    [Fact]
    public async Task Operations_ReportUnavailable_AfterTheHostStops()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var store = host.Services.GetRequiredService<IJobStore>();
        var election = host.Services.GetRequiredService<ILeaderElection>();
        var lease = (await election.TryAcquireAsync(new JobName("db.lease"), TimeSpan.FromSeconds(30), CancellationToken.None)).ValueOrThrow();
        Assert.True(lease.IsHeld);

        await host.App.StopAsync(CancellationToken.None);
        Assert.Equal(SchedulingErrors.StoreUnavailable, (await store.ListJobsAsync(CancellationToken.None)).Error?.Code);
        Assert.Equal(SchedulingErrors.StoreUnavailable, (await store.SaveJobsAsync([new JobRecord { Name = new JobName("db.stopped"), JobTypeName = "t" }], CancellationToken.None)).Error?.Code);
        Assert.Equal(SchedulingErrors.StoreUnavailable, (await store.AcquireDueAsync(DateTimeOffset.UtcNow, 1, "n", DateTimeOffset.UtcNow, CancellationToken.None)).Error?.Code);
        Assert.Equal(SchedulingErrors.StoreUnavailable, Assert.Single(await store.RequestCancelAsync(["x"], CancellationToken.None)).Error?.Code);
        Assert.Equal(SchedulingErrors.LeaseUnavailable, (await election.TryAcquireAsync(new JobName("db.lease.other"), TimeSpan.FromSeconds(30), CancellationToken.None)).Error?.Code);
        // 存储不可达不等于失去租约：到期前仍按持有对待
        Assert.Equal(SchedulingErrors.LeaseUnavailable, (await lease.RenewAsync(CancellationToken.None)).Error?.Code);
        Assert.True(lease.IsHeld);
        await lease.ReleaseAsync();
        Assert.False(lease.IsHeld);
    }

    /// <summary>
    /// 一个节点的引擎配置：节点 ID、轮询周期与 0.2 秒的维护周期。
    /// </summary>
    private static List<KeyValuePair<string, string?>> NodeSettings(string nodeId, string pollIntervalSeconds) =>
    [
        new($"{SchedulerOptions.ConfigurationSection}:NodeId", nodeId),
        new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", pollIntervalSeconds),
        new($"{SchedulerOptions.ConfigurationSection}:HousekeepingIntervalSeconds", "0.2"),
    ];

    /// <summary>
    /// 登记多节点用例的记录与作业类型；作业本身在两个节点都启动后由用例在运行时登记。
    /// </summary>
    private static void Register(IServiceCollection services, OverlapLog log)
    {
        services.AddSingleton(log);
        services.AddJobType<OverlapProbeJob>();
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
    /// </summary>
    private static async Task WaitUntilAsync(Func<ValueTask<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
