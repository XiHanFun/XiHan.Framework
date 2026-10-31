// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <see cref="IJobStore"/> 的一致性套件。
/// </summary>
/// <remarks>
/// 查询与排序、抢占的原子性、独占运行与阻塞、历史、节点与恢复。Provider 的测试项目继承并实现 <see cref="CreateAsync"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class JobStoreConformance
{
    /// <summary>
    /// 创建被测存储及其释放句柄。
    /// </summary>
    protected abstract Task<(IJobStore Store, IAsyncDisposable Host)> CreateAsync();

    /// <summary>
    /// 作业的写入、查找、列表、替换、级联删除与快照语义。
    /// </summary>
    [Fact]
    public async Task Jobs_SaveFindListDelete_CascadeTriggers_AndSnapshot()
    {
        var (store, host) = await CreateAsync().ConfigureAwait(false);
        await using (host.ConfigureAwait(false))
        {
            var a = Job("conf.store.a");
            var b = Job("conf.store.b");
            Assert.True((await store.SaveJobsAsync([b, a], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await store.SaveTriggersAsync([Trigger(a.Name, "t1", DateTimeOffset.UtcNow), Trigger(a.Name, "t2", DateTimeOffset.UtcNow), Trigger(b.Name, "t1", DateTimeOffset.UtcNow)], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal(["conf.store.a", "conf.store.b"], (await store.ListJobsAsync(CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static job => job.Name.Value));
            var found = (await store.FindJobAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.Equal((a.JobTypeName, "v", true, JobSource.Runtime), (found.JobTypeName, found.Data["k"]!.GetValue<string>(), found.AllowConcurrent, found.Source));
            // 快照：改读出来的对象不影响存储
            found.Paused = true;
            found.Data["k"] = "changed";
            var again = (await store.FindJobAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.False(again.Paused);
            Assert.Equal("v", again.Data["k"]!.GetValue<string>());
            Assert.False((await store.FindJobAsync(new JobName("conf.store.none"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            // 替换定义、暂停状态、来源与代码定义的指纹；占用的运行 ID 由存储保留
            a.Paused = true;
            a.Source = JobSource.Overridden;
            a.CodeFingerprint = "F00D";
            a.Retry = JobRetryPolicy.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
            a.Timeout = TimeSpan.FromSeconds(45);
            Assert.True((await store.SaveJobsAsync([a], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var overridden = (await store.FindJobAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.Equal((true, JobSource.Overridden, "F00D"), (overridden.Paused, overridden.Source, overridden.CodeFingerprint));
            // 重试策略与执行时限随作业保存（ADR-0060）
            Assert.Equal((a.Retry, TimeSpan.FromSeconds(45)), (overridden.Retry, overridden.Timeout));
            var occupying = Run(b.Name, "node-a", DateTimeOffset.UtcNow);
            Assert.True((await store.TryBeginRunAsync(occupying, true, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            b.Description = "replaced";
            b.ActiveRunId = null;
            Assert.True((await store.SaveJobsAsync([b], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var replaced = (await store.FindJobAsync(b.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.Equal(("replaced", occupying.Id), (replaced.Description, replaced.ActiveRunId));
            Assert.Equal(["conf.store.a/t1", "conf.store.a/t2", "conf.store.b/t1"], (await store.TriggersAsync(null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static trigger => trigger.Key.ToString()));
            Assert.Equal(["t1", "t2"], (await store.TriggersAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static trigger => trigger.Name));
            Assert.True((await store.DeleteTriggersAsync([new TriggerKey(a.Name, "t2"), new TriggerKey(a.Name, "missing")], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal(["t1"], (await store.TriggersAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static trigger => trigger.Name));
            // 删除作业级联触发器，不存在的忽略
            Assert.True((await store.DeleteJobsAsync([a.Name, new JobName("conf.store.none")], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.False((await store.FindJobAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Empty((await store.TriggersAsync(a.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Single((await store.TriggersAsync(b.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        }
    }

    /// <summary>
    /// 触发器记录的全部字段往返。
    /// </summary>
    [Fact]
    public async Task Triggers_RoundTripAllFields()
    {
        var (store, host) = await CreateAsync().ConfigureAwait(false);
        await using (host.ConfigureAwait(false))
        {
            var job = Job("conf.store.fields");
            Assert.True((await store.SaveJobsAsync([job], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var now = DateTimeOffset.UtcNow;
            var trigger = new TriggerRecord
            {
                Job = job.Name,
                Name = "full",
                Schedule = JobSchedule.FromCron("0 0 9 * * MON-FRI", "Asia/Shanghai") with { StartAt = now, EndAt = now.AddDays(30), Calendar = "holidays" },
                Data = new JsonObject { ["n"] = 1, ["nested"] = new JsonObject { ["x"] = true } },
                Priority = 7,
                MisfirePolicy = MisfirePolicy.RunAll,
                Description = "描述",
                State = TriggerState.Blocked,
                NextFireAt = now.AddMinutes(1),
                PreviousFireAt = now.AddMinutes(-1),
                AcquiredBy = "node-x",
                AcquiredAt = now,
                FireCount = 42,
                Error = "why",
                Local = true,
                Transient = true,
                RunAs = JobTrigger.Retry,
                Attempt = 3,
                RetryOf = "0123456789abcdef0123456789abcdef",
            };
            Assert.True((await store.SaveTriggersAsync([trigger], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var stored = Assert.Single((await store.TriggersAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(trigger.Schedule.ToString(), stored.Schedule.ToString());
            Assert.Equal(trigger.Data.ToJsonString(), stored.Data.ToJsonString());
            Assert.Equal((7, MisfirePolicy.RunAll, "描述", TriggerState.Blocked, "node-x", 42L, "why", true, true, JobTrigger.Retry), (stored.Priority, stored.MisfirePolicy, stored.Description, stored.State, stored.AcquiredBy, stored.FireCount, stored.Error, stored.Local, stored.Transient, stored.RunAs));
            Assert.Equal((3, trigger.RetryOf), (stored.Attempt, stored.RetryOf));
            Assert.Equal((trigger.NextFireAt, trigger.PreviousFireAt, trigger.AcquiredAt), (stored.NextFireAt, stored.PreviousFireAt, stored.AcquiredAt));
        }
    }

    /// <summary>
    /// 抢占的顺序、原子性与跳过未到期 / 暂停 / 本地 / 阻塞的触发器。
    /// </summary>
    [Fact]
    public async Task AcquireDue_IsAtomicAcrossNodes_OrderedByDueThenPriority_AndSkipsOthers()
    {
        var (store, host) = await CreateAsync().ConfigureAwait(false);
        await using (host.ConfigureAwait(false))
        {
            var job = Job("conf.store.due");
            Assert.True((await store.SaveJobsAsync([job], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var now = DateTimeOffset.UtcNow;
            var triggers = new List<TriggerRecord>
            {
                Trigger(job.Name, "late-low", now.AddSeconds(-1), priority: 1),
                Trigger(job.Name, "late-high", now.AddSeconds(-1), priority: 9),
                Trigger(job.Name, "early", now.AddSeconds(-10)),
                Trigger(job.Name, "future", now.AddMinutes(5)),
                Trigger(job.Name, "paused", now.AddSeconds(-5), state: TriggerState.Paused),
                Trigger(job.Name, "local", now.AddSeconds(-5), local: true),
                Trigger(job.Name, "blocked", now.AddSeconds(-5), state: TriggerState.Blocked),
            };
            for (var index = 0; index < 20; index++)
            {
                triggers.Add(Trigger(job.Name, $"bulk-{index:D2}", now.AddMilliseconds(-500)));
            }

            Assert.True((await store.SaveTriggersAsync(triggers, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            // 顺序：到期时刻升序，再优先级降序
            var first = (await store.AcquireDueAsync(now, 3, "node-a", now, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(["early", "late-high", "late-low"], first.Select(static trigger => trigger.Name));
            Assert.All(first, trigger => Assert.Equal((TriggerState.Acquired, "node-a", now), (trigger.State, trigger.AcquiredBy, trigger.AcquiredAt)));
            // 多个节点同时抢占：每条只被一个节点拿到
            var tasks = Enumerable.Range(0, 4).Select(index => store.AcquireDueAsync(now, 10, "node-" + index, now, CancellationToken.None).AsTask()).ToArray();
            var acquired = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(static result => result.ValueOrThrow()).ToList();
            Assert.Equal(20, acquired.Count);
            Assert.Equal(20, acquired.Select(static trigger => trigger.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.All(acquired, static trigger => Assert.StartsWith("bulk-", trigger.Name, StringComparison.Ordinal));
            Assert.Empty((await store.AcquireDueAsync(now, 10, "node-z", now, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            var states = (await store.TriggersAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ToDictionary(static trigger => trigger.Name, static trigger => trigger.State, StringComparer.Ordinal);
            Assert.Equal((TriggerState.Waiting, TriggerState.Paused, TriggerState.Waiting, TriggerState.Blocked), (states["future"], states["paused"], states["local"], states["blocked"]));
        }
    }

    /// <summary>
    /// 独占开始、完成释放与解除阻塞、历史查询与计数、最近一次、取消请求与清理。
    /// </summary>
    [Fact]
    public async Task Runs_ExclusiveBegin_CompleteUnblocks_QueryLastCancelPurge()
    {
        var (store, host) = await CreateAsync().ConfigureAwait(false);
        await using (host.ConfigureAwait(false))
        {
            var job = Job("conf.store.runs");
            var other = Job("conf.store.other");
            Assert.True((await store.SaveJobsAsync([job, other], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var now = DateTimeOffset.UtcNow;
            var blocked = Trigger(job.Name, "blocked", now, state: TriggerState.Blocked);
            Assert.True((await store.SaveTriggersAsync([blocked], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal(SchedulingErrors.JobNotFound, (await store.TryBeginRunAsync(Run(new JobName("conf.store.none"), "node-a", now), true, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            var first = Run(job.Name, "node-a", now);
            var second = Run(job.Name, "node-b", now.AddSeconds(1));
            var third = Run(job.Name, "node-b", now.AddSeconds(2));
            third.Attempt = 2;
            third.RetryOf = first.Id;
            Assert.True((await store.TryBeginRunAsync(first, true, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.False((await store.TryBeginRunAsync(second, true, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.True((await store.TryBeginRunAsync(third, false, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(first.Id, (await store.FindJobAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().ActiveRunId);
            Assert.Equal([first.Id, third.Id], (await store.RunningAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            // 尝试次数与首次运行 ID 随运行保存
            var retried = (await store.RunningAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Single(run => run.Id == third.Id);
            Assert.Equal((2, first.Id, 1), (retried.Attempt, retried.RetryOf, (await store.RunningAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Single(run => run.Id == first.Id).Attempt));
            Assert.Equal(2, (await store.RunningAsync(null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
            // 取消请求只对运行中的有效，按节点取回
            var cancel = await store.RequestCancelAsync([third.Id, "nope"], CancellationToken.None).ConfigureAwait(false);
            Assert.True(cancel[0].IsSuccess);
            Assert.Equal(SchedulingErrors.RunNotFound, cancel[1].Error?.Code);
            Assert.Equal([third.Id], (await store.CancelRequestedRunsAsync("node-b", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Empty((await store.CancelRequestedRunsAsync("node-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            // 完成：释放独占、阻塞的触发器回到等待
            first.State = JobRunState.Failed;
            first.CompletedAt = now.AddSeconds(3);
            first.ErrorCode = "X_001";
            first.ErrorMessage = "boom";
            Assert.True((await store.CompleteRunAsync(first, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Null((await store.FindJobAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().ActiveRunId);
            Assert.Equal(TriggerState.Waiting, Assert.Single((await store.TriggersAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).State);
            Assert.True((await store.TryBeginRunAsync(second, true, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            third.State = JobRunState.Canceled;
            third.CompletedAt = now.AddSeconds(4);
            Assert.True((await store.CompleteRunAsync(third, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            second.State = JobRunState.Succeeded;
            second.CompletedAt = now.AddSeconds(5);
            Assert.True((await store.CompleteRunAsync(second, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            // 查询：计划时刻降序、状态过滤、分页、时间范围
            var all = (await store.RunsAsync(new JobRunQuery { Job = job.Name }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal([third.Id, second.Id, first.Id], all.Select(static run => run.Id));
            Assert.Equal(("X_001", "boom", JobRunState.Failed, "node-a"), (all[2].ErrorCode, all[2].ErrorMessage, all[2].State, all[2].NodeId));
            Assert.Equal([second.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, States = [JobRunState.Succeeded] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal([second.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, Skip = 1, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal([second.Id, first.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, From = now, To = now.AddSeconds(2) }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal("v", all[0].Data["k"]!.GetValue<string>());
            // 触发器名、执行节点、有无错误；总数与列表同一组过滤项，不看分页
            Assert.Equal([third.Id, second.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, Node = "node-b" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal([first.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, HasError = true }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal([third.Id, second.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name, HasError = false }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Empty((await store.RunsAsync(new JobRunQuery { Job = job.Name, Trigger = "other" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(3, (await store.CountRunsAsync(new JobRunQuery { Job = job.Name, Trigger = "t", Skip = 1, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(1, (await store.CountRunsAsync(new JobRunQuery { Job = job.Name, States = [JobRunState.Succeeded] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(2, (await store.CountRunsAsync(new JobRunQuery { Job = job.Name, Node = "node-b", HasError = false }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(0, (await store.CountRunsAsync(new JobRunQuery { Job = new JobName("conf.store.none") }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            // 最近一次已结束的运行按结束时刻取
            var last = Assert.Single((await store.LastRunsAsync([job.Name, other.Name], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(second.Id, last.Id);
            // 清理早于某时刻结束的
            Assert.Equal(2, (await store.PurgeRunsAsync(now.AddSeconds(4.5), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal([second.Id], (await store.RunsAsync(new JobRunQuery { Job = job.Name }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
        }
    }

    /// <summary>
    /// 节点心跳、列表与失联恢复。
    /// </summary>
    [Fact]
    public async Task Nodes_HeartbeatList_AndRecoverLostNodes()
    {
        var (store, host) = await CreateAsync().ConfigureAwait(false);
        await using (host.ConfigureAwait(false))
        {
            var job = Job("conf.store.nodes");
            Assert.True((await store.SaveJobsAsync([job], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var now = DateTimeOffset.UtcNow;
            Assert.True((await store.HeartbeatAsync(new SchedulerNodeRecord { Id = "node-b", StartedAt = now, LastHeartbeatAt = now }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await store.HeartbeatAsync(new SchedulerNodeRecord { Id = "node-a", StartedAt = now, LastHeartbeatAt = now }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await store.HeartbeatAsync(new SchedulerNodeRecord { Id = "node-a", StartedAt = now, LastHeartbeatAt = now.AddSeconds(5) }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var nodes = (await store.NodesAsync(CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(["node-a", "node-b"], nodes.Select(static node => node.Id));
            Assert.Equal(now.AddSeconds(5), nodes[0].LastHeartbeatAt);
            var acquiredByDead = Trigger(job.Name, "dead", now.AddSeconds(-1), state: TriggerState.Acquired);
            acquiredByDead.AcquiredBy = "node-b";
            acquiredByDead.AcquiredAt = now;
            acquiredByDead.NextFireAt = null;
            var acquiredByAlive = Trigger(job.Name, "alive", now.AddSeconds(-1), state: TriggerState.Acquired);
            acquiredByAlive.AcquiredBy = "node-a";
            Assert.True((await store.SaveTriggersAsync([acquiredByDead, acquiredByAlive], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            var deadRun = Run(job.Name, "node-b", now);
            var aliveRun = Run(job.Name, "node-a", now);
            Assert.True((await store.TryBeginRunAsync(deadRun, true, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.True((await store.TryBeginRunAsync(aliveRun, false, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            var recovered = (await store.RecoverNodesAsync(["node-b"], now.AddSeconds(10), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            var lost = Assert.Single(recovered);
            Assert.Equal((deadRun.Id, JobRunState.Lost, now.AddSeconds(10)), (lost.Id, lost.State, lost.CompletedAt));
            Assert.Equal(["node-a"], (await store.NodesAsync(CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static node => node.Id));
            var triggers = (await store.TriggersAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ToDictionary(static trigger => trigger.Name, StringComparer.Ordinal);
            Assert.Equal((TriggerState.Waiting, now.AddSeconds(10)), (triggers["dead"].State, triggers["dead"].NextFireAt));
            Assert.Null(triggers["dead"].AcquiredBy);
            Assert.Equal((TriggerState.Acquired, "node-a"), (triggers["alive"].State, triggers["alive"].AcquiredBy));
            Assert.Null((await store.FindJobAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().ActiveRunId);
            Assert.Equal([aliveRun.Id], (await store.RunningAsync(job.Name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static run => run.Id));
            Assert.Equal(JobRunState.Lost, (await store.RunsAsync(new JobRunQuery { Job = job.Name, States = [JobRunState.Lost] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Single().State);
        }
    }

    /// <summary>
    /// 作业记录样本。
    /// </summary>
    protected static JobRecord Job(string name) => new()
    {
        Name = new JobName(name),
        JobTypeName = "Conformance.Job",
        Data = new JsonObject { ["k"] = "v" },
        AllowConcurrent = true,
        Source = JobSource.Runtime,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// 触发器记录样本。
    /// </summary>
    protected static TriggerRecord Trigger(JobName job, string name, DateTimeOffset nextFireAt, int priority = JobTriggerDefinition.DefaultPriority, TriggerState state = TriggerState.Waiting, bool local = false) => new()
    {
        Job = job,
        Name = name,
        Schedule = JobSchedule.Every(TimeSpan.FromMinutes(1)),
        Priority = priority,
        State = state,
        NextFireAt = nextFireAt,
        Local = local,
    };

    /// <summary>
    /// 运行记录样本。
    /// </summary>
    protected static JobRunRecord Run(JobName job, string nodeId, DateTimeOffset scheduledAt) => new()
    {
        Id = JobRun.NewId(),
        Job = job,
        Trigger = JobTrigger.Schedule,
        TriggerName = "t",
        ScheduledAt = scheduledAt,
        StartedAt = scheduledAt,
        State = JobRunState.Running,
        NodeId = nodeId,
        Data = new JsonObject { ["k"] = "v" },
    };
}
