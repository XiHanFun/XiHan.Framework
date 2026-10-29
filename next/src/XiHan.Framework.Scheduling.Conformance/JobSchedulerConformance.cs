// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <c>scheduling.jobs@1</c> 的一致性套件。
/// </summary>
/// <remarks>
/// Provider 的测试项目继承并实现 <see cref="StartAsync"/>，装配被测 Provider（轮询周期取 50 毫秒左右）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class JobSchedulerConformance
{
    /// <summary>
    /// 每 100 毫秒的作业。
    /// </summary>
    protected static JobName IntervalJob => new("conformance.interval");

    /// <summary>
    /// 只手动触发的作业。
    /// </summary>
    protected static JobName ManualJob => new("conformance.manual");

    /// <summary>
    /// 允许并发的手动作业。
    /// </summary>
    protected static JobName ConcurrentJob => new("conformance.concurrent");

    /// <summary>
    /// 等待运行发生的上限。
    /// </summary>
    protected virtual TimeSpan WaitTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 启动装配了被测 Provider 的宿主。
    /// </summary>
    /// <remarks>
    /// <paramref name="configuration"/> 里是用例要求的引擎配置（如 misfire 阈值）。
    /// </remarks>
    protected abstract Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null);

    /// <summary>
    /// 手动触发：运行、关联 ID、状态与历史里的结果。
    /// </summary>
    [Fact]
    public async Task Trigger_RunsTheJob_AndReportsOutcomeAndStatus()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var outcome = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.True(outcome.IsSuccess);
        var (run, correlation, origin) = Assert.Single(log.Runs);
        Assert.Equal((ManualJob, JobTrigger.Manual), (run.Name, run.Trigger));
        Assert.Equal(run.Id, correlation);
        Assert.Equal(new InvocationOrigin { Protocol = InvocationProtocols.Job, Target = ManualJob.Value }, origin);
        var status = await StatusAsync(host, ManualJob).ConfigureAwait(false);
        Assert.Null(status.NextRun);
        Assert.Empty(status.Triggers);
        Assert.False(status.IsRunning);
        Assert.False(status.Paused);
        Assert.Equal(run.Id, status.Last!.Run.Id);
        Assert.True(status.Last.Result.IsSuccess);
        Assert.True(status.Last.CompletedAt >= status.Last.StartedAt);
        Assert.Contains((await host.Scheduler.StatusesAsync(CancellationToken.None).ConfigureAwait(false)).ValueOrThrow(), s => s.Name == ManualJob);
        // 失败的结果原样返回并记入状态与历史
        log.FailWith = Error.Conflict("CONFORMANCE_CONFLICT_001", "conformance.job.rejected");
        var failed = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal("CONFORMANCE_CONFLICT_001", failed.Error?.Code);
        Assert.Equal("CONFORMANCE_CONFLICT_001", (await StatusAsync(host, ManualJob).ConfigureAwait(false)).Last!.Result.Error?.Code);
        var history = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([JobRunState.Failed, JobRunState.Succeeded], history.Select(static r => r.State));
        Assert.Equal("CONFORMANCE_CONFLICT_001", history[0].ErrorCode);
    }

    /// <summary>
    /// 批量触发逐项结果。
    /// </summary>
    /// <remarks>
    /// 未登记的作业；手动触发的数据覆盖作业数据。
    /// </remarks>
    [Fact]
    public async Task Trigger_UnknownJob_IsNotFound_AndBatchReturnsPerItem()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var results = await host.Scheduler.TriggerAsync([ManualJob, new JobName("conformance.missing"), ManualJob], CancellationToken.None).ConfigureAwait(false);
        Assert.Equal([ManualJob, new JobName("conformance.missing"), ManualJob], results.Select(static r => r.Name));
        Assert.True(results[0].Outcome.IsSuccess);
        Assert.Equal(SchedulingErrors.JobNotFound, results[1].Outcome.Error?.Code);
        Assert.True(results[2].Outcome.IsSuccess);
        Assert.Equal(2, log.Runs.Count);
        Assert.Empty(await host.Scheduler.TriggerAsync([], CancellationToken.None).ConfigureAwait(false));
        Assert.False((await host.Scheduler.StatusAsync(new JobName("conformance.missing"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        // 手动触发可带数据：覆盖作业数据的同名键，其余保留
        var outcome = await host.Scheduler.TriggerAsync(ManualJob, new JsonObject { ["who"] = "manual" }, CancellationToken.None).ConfigureAwait(false);
        Assert.True(outcome.IsSuccess);
        var data = log.Runs[^1].Run.Data;
        Assert.Equal(("manual", "job"), (data["who"]!.GetValue<string>(), data["origin"]!.GetValue<string>()));
    }

    /// <summary>
    /// 间隔计划反复运行并更新触发器状态。
    /// </summary>
    [Fact]
    public async Task IntervalSchedule_RunsRepeatedly()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await log.WaitAsync(IntervalJob, 2, WaitTimeout).ConfigureAwait(false);
        Assert.All(log.Runs.Where(r => r.Run.Name == IntervalJob), static r => Assert.Equal((JobTrigger.Schedule, JobTriggerDefinition.DefaultName), (r.Run.Trigger, r.Run.TriggerName)));
        var status = await StatusAsync(host, IntervalJob).ConfigureAwait(false);
        Assert.NotNull(status.Last);
        Assert.True(status.NextRun is not null || status.IsRunning);
        var trigger = Assert.Single(status.Triggers);
        Assert.Equal(JobTriggerDefinition.DefaultName, trigger.Name);
        Assert.True(trigger.FireCount >= 2);
        Assert.NotNull(trigger.PreviousFireAt);
    }

    /// <summary>
    /// 不允许并发的作业运行中再触发返回冲突。
    /// </summary>
    /// <remarks>
    /// 允许并发的可以同时运行。
    /// </remarks>
    [Fact]
    public async Task ConcurrentTrigger_ConflictsUnlessAllowed()
    {
        var log = new RecordingJobLog { Delay = TimeSpan.FromMilliseconds(400) };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var first = host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).AsTask();
        await log.WaitAsync(ManualJob, 1, WaitTimeout).ConfigureAwait(false);
        Assert.True((await StatusAsync(host, ManualJob).ConfigureAwait(false)).IsRunning);
        var second = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.JobRunning, second.Error?.Code);
        Assert.True((await first.ConfigureAwait(false)).IsSuccess);
        var a = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        var b = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        Assert.All(await Task.WhenAll(a, b).ConfigureAwait(false), static r => Assert.True(r.IsSuccess));
        Assert.Equal(2, log.Runs.Count(r => r.Run.Name == ConcurrentJob));
    }

    /// <summary>
    /// 抛出异常的作业是失败结果，之后仍可运行。
    /// </summary>
    [Fact]
    public async Task ThrowingJob_IsAFailureResult()
    {
        var log = new RecordingJobLog { Throw = true };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var thrown = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.True(thrown.IsFailure);
        Assert.False((await StatusAsync(host, ManualJob).ConfigureAwait(false)).IsRunning);
        log.Throw = false;
        Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 运行时登记、暂停与恢复、替换与删除。
    /// </summary>
    /// <remarks>
    /// 类型未登记、触发器名重复、日历未登记被拒绝。
    /// </remarks>
    [Fact]
    public async Task Schedule_AddsRuntimeJob_PauseResume_AndUnschedule()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var dynamic = new JobName("conformance.dynamic");
        var definition = new JobDefinition(dynamic, JobSchedule.Every(TimeSpan.FromMilliseconds(100)), typeof(RecordingJob)) { Data = new JsonObject { ["k"] = "v" } };
        Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await log.WaitAsync(dynamic, 2, WaitTimeout).ConfigureAwait(false);
        Assert.Equal("v", log.Runs.First(r => r.Run.Name == dynamic).Run.Data["k"]!.GetValue<string>());
        Assert.Equal(JobSource.Runtime, (await StatusAsync(host, dynamic).ConfigureAwait(false)).Job.Source);
        // 暂停：不再按计划运行，手动触发仍可用；恢复后继续
        Assert.True((await host.Scheduler.PauseAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await WaitUntilAsync(async () => !(await StatusAsync(host, dynamic).ConfigureAwait(false)).IsRunning).ConfigureAwait(false);
        var paused = await StatusAsync(host, dynamic).ConfigureAwait(false);
        Assert.True(paused.Paused);
        Assert.Equal(TriggerState.Paused, Assert.Single(paused.Triggers).State);
        var before = log.Runs.Count(r => r.Run.Name == dynamic);
        await Task.Delay(350).ConfigureAwait(false);
        Assert.Equal(before, log.Runs.Count(r => r.Run.Name == dynamic));
        Assert.True((await host.Scheduler.TriggerAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Scheduler.ResumeAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await log.WaitAsync(dynamic, before + 3, WaitTimeout).ConfigureAwait(false);
        Assert.False((await StatusAsync(host, dynamic).ConfigureAwait(false)).Paused);
        // 替换：同名登记换计划与数据，保留作业；删除后未登记
        var replaced = definition with { Triggers = [new JobTriggerDefinition("slow", JobSchedule.Every(TimeSpan.FromHours(1)))], Data = new JsonObject { ["k"] = "w" } };
        Assert.True((await host.Scheduler.ScheduleAsync(replaced, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var status = await StatusAsync(host, dynamic).ConfigureAwait(false);
        Assert.Equal("slow", Assert.Single(status.Triggers).Name);
        Assert.Equal("w", status.Job.Data["k"]!.GetValue<string>());
        Assert.True((await host.Scheduler.UnscheduleAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.False((await host.Scheduler.StatusAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Equal(SchedulingErrors.JobNotFound, (await host.Scheduler.UnscheduleAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(SchedulingErrors.JobNotFound, (await host.Scheduler.PauseAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        // 未登记的类型、重复的触发器名、未登记的日历都拒绝
        Assert.Equal(SchedulingErrors.JobTypeNotRegistered, (await host.Scheduler.ScheduleAsync(new JobDefinition(new JobName("conformance.unknown"), JobSchedule.Manual, typeof(UnregisteredJob)), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        var duplicated = new JobDefinition(dynamic, typeof(RecordingJob)) { Triggers = [new JobTriggerDefinition("a", JobSchedule.Every(TimeSpan.FromMinutes(1))), new JobTriggerDefinition("a", JobSchedule.Every(TimeSpan.FromMinutes(2)))] };
        Assert.Equal(SchedulingErrors.DefinitionInvalid, (await host.Scheduler.ScheduleAsync(duplicated, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        var noCalendar = new JobDefinition(dynamic, JobSchedule.Every(TimeSpan.FromMinutes(1)) with { Calendar = "conformance.none" }, typeof(RecordingJob));
        Assert.Equal(SchedulingErrors.DefinitionInvalid, (await host.Scheduler.ScheduleAsync(noCalendar, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 一个作业多个触发器：各自的数据与次数，单个触发器的暂停与恢复。
    /// </summary>
    [Fact]
    public async Task MultipleTriggers_HavePriorityData_AndCanBePausedIndividually()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var multi = new JobName("conformance.multi");
        var definition = new JobDefinition(multi, typeof(RecordingJob))
        {
            AllowConcurrent = true,
            Data = new JsonObject { ["origin"] = "job", ["t"] = "none" },
            Triggers =
            [
                new JobTriggerDefinition("fast", JobSchedule.Every(TimeSpan.FromMilliseconds(100))) { Data = new JsonObject { ["t"] = "fast" }, Priority = 1 },
                new JobTriggerDefinition("once", JobSchedule.Once(DateTimeOffset.UtcNow.AddMilliseconds(200))) { Data = new JsonObject { ["t"] = "once" }, Priority = 9 },
            ],
        };
        Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await WaitUntilAsync(() => Task.FromResult(log.Runs.Any(r => r.Run.Name == multi && r.Run.TriggerName == "once") && log.Runs.Count(r => r.Run.Name == multi && r.Run.TriggerName == "fast") >= 2)).ConfigureAwait(false);
        var once = Assert.Single(log.Runs, r => r.Run.Name == multi && r.Run.TriggerName == "once");
        Assert.Equal(("once", "job"), (once.Run.Data["t"]!.GetValue<string>(), once.Run.Data["origin"]!.GetValue<string>()));
        Assert.Equal("fast", log.Runs.First(r => r.Run.Name == multi && r.Run.TriggerName == "fast").Run.Data["t"]!.GetValue<string>());
        await WaitUntilAsync(async () => (await StatusAsync(host, multi).ConfigureAwait(false)).Triggers.Single(static t => t.Name == "once").State == TriggerState.Complete).ConfigureAwait(false);
        var status = await StatusAsync(host, multi).ConfigureAwait(false);
        Assert.Equal(["fast", "once"], status.Triggers.Select(static t => t.Name));
        Assert.Equal(1, status.Triggers.Single(static t => t.Name == "once").FireCount);
        // 单个触发器的暂停与恢复
        var key = new TriggerKey(multi, "fast");
        Assert.True((await host.Scheduler.PauseTriggersAsync([key], CancellationToken.None).ConfigureAwait(false))[0].IsSuccess);
        Assert.Equal(TriggerState.Paused, (await StatusAsync(host, multi).ConfigureAwait(false)).Triggers.Single(static t => t.Name == "fast").State);
        Assert.False((await StatusAsync(host, multi).ConfigureAwait(false)).Paused);
        Assert.True((await host.Scheduler.ResumeTriggersAsync([key], CancellationToken.None).ConfigureAwait(false))[0].IsSuccess);
        await WaitUntilAsync(async () => (await StatusAsync(host, multi).ConfigureAwait(false)).Triggers.Single(static t => t.Name == "fast").State is TriggerState.Waiting or TriggerState.Acquired).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.TriggerNotFound, (await host.Scheduler.PauseTriggersAsync([new TriggerKey(multi, "nope")], CancellationToken.None).ConfigureAwait(false))[0].Error?.Code);
    }

    /// <summary>
    /// 恢复后按 misfire 策略补运行或跳过。
    /// </summary>
    [Fact]
    public async Task Misfire_PoliciesApplyAfterResume()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log), [new("XiHan:Scheduling:MisfireThresholdSeconds", "0.2")]).ConfigureAwait(false);
        var runOnce = new JobName("conformance.misfire.once");
        var runAll = new JobName("conformance.misfire.all");
        var skip = new JobName("conformance.misfire.skip");
        foreach (var (name, policy) in new[] { (runOnce, MisfirePolicy.RunOnce), (runAll, MisfirePolicy.RunAll), (skip, MisfirePolicy.Skip) })
        {
            var definition = new JobDefinition(name, typeof(RecordingJob)) { Triggers = [new JobTriggerDefinition("t", JobSchedule.Every(TimeSpan.FromMilliseconds(200))) { MisfirePolicy = policy }] };
            Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        foreach (var name in new[] { runOnce, runAll, skip })
        {
            await log.WaitAsync(name, 1, WaitTimeout).ConfigureAwait(false);
            Assert.True((await host.Scheduler.PauseAsync(name, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        // 暂停期间到期时刻过去了远超阈值的时间
        await Task.Delay(800).ConfigureAwait(false);
        var counts = new[] { runOnce, runAll, skip }.ToDictionary(static n => n, n => log.Runs.Count(r => r.Run.Name == n));
        var resumedAt = DateTimeOffset.UtcNow;
        foreach (var name in new[] { runOnce, runAll, skip })
        {
            Assert.True((await host.Scheduler.ResumeAsync(name, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        // Skip：恢复后下一次从现在推算（不在过去）
        await WaitUntilAsync(async () => (await StatusAsync(host, skip).ConfigureAwait(false)) is { } skipped && (skipped.NextRun is null || skipped.NextRun >= resumedAt.AddMilliseconds(-50))).ConfigureAwait(false);
        await log.WaitAsync(runOnce, counts[runOnce] + 1, WaitTimeout).ConfigureAwait(false);
        await log.WaitAsync(runAll, counts[runAll] + 1, WaitTimeout).ConfigureAwait(false);
        // RunOnce：补的那次按"现在"计划；RunAll：补的那次按原计划时刻（在恢复之前）
        var onceRun = log.Runs.Where(r => r.Run.Name == runOnce).Skip(counts[runOnce]).First().Run;
        var allRun = log.Runs.Where(r => r.Run.Name == runAll).Skip(counts[runAll]).First().Run;
        Assert.True(onceRun.ScheduledAt >= resumedAt.AddMilliseconds(-50), "RunOnce reschedules the missed fire to now.");
        Assert.True(allRun.ScheduledAt < resumedAt, "RunAll keeps the original fire time.");
    }

    /// <summary>
    /// 入队的运行由集群运行一次，不等待。
    /// </summary>
    [Fact]
    public async Task Enqueue_RunsOnceWithoutWaiting()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var enqueued = await host.Scheduler.EnqueueAsync(ManualJob, new JsonObject { ["who"] = "queue" }, CancellationToken.None).ConfigureAwait(false);
        Assert.StartsWith(JobSchedulerEngine.ManualTriggerPrefix, enqueued.ValueOrThrow(), StringComparison.Ordinal);
        await log.WaitAsync(ManualJob, 1, WaitTimeout).ConfigureAwait(false);
        var (run, _, _) = Assert.Single(log.Runs);
        Assert.Equal((JobTrigger.Manual, "queue"), (run.Trigger, run.Data["who"]!.GetValue<string>()));
        Assert.Null(run.TriggerName);
        await WaitUntilAsync(async () => (await StatusAsync(host, ManualJob).ConfigureAwait(false)).Triggers.Count == 0).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.JobNotFound, (await host.Scheduler.EnqueueAsync(new JobName("conformance.missing"), null, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 取消运行：作业的令牌被取消，历史记为取消。
    /// </summary>
    [Fact]
    public async Task CancelRun_StopsTheJob_AndRecordsCanceled()
    {
        var log = new RecordingJobLog { Delay = TimeSpan.FromSeconds(5) };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var triggered = host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).AsTask();
        await log.WaitAsync(ManualJob, 1, WaitTimeout).ConfigureAwait(false);
        var running = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, States = [JobRunState.Running] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var run = Assert.Single(running);
        Assert.Equal(log.Runs[0].Run.Id, run.Id);
        Assert.True((await host.Scheduler.CancelRunAsync(run.Id, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var outcome = await triggered.ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.RunCanceled, outcome.Error?.Code);
        var history = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(JobRunState.Canceled, Assert.Single(history).State);
        Assert.Equal(SchedulingErrors.RunNotFound, (await host.Scheduler.CancelRunAsync(run.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(SchedulingErrors.RunNotFound, (await host.Scheduler.CancelRunAsync("nope", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 运行历史按作业、状态、时间范围与分页查询。
    /// </summary>
    [Fact]
    public async Task History_IsQueryable_WithPagingAndStates()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        for (var index = 0; index < 3; index++)
        {
            Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        log.FailWith = Error.Conflict("CONFORMANCE_CONFLICT_001", "conformance.job.rejected");
        Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsFailure);
        var all = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(4, all.Count);
        Assert.True(all.Zip(all.Skip(1)).All(static pair => pair.First.ScheduledAt >= pair.Second.ScheduledAt));
        Assert.All(all, static run => Assert.NotNull(run.CompletedAt));
        var page = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, Skip = 1, Limit = 2 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(all.Skip(1).Take(2).Select(static r => r.Id), page.Select(static r => r.Id));
        var failed = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, States = [JobRunState.Failed] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("CONFORMANCE_CONFLICT_001", Assert.Single(failed).ErrorCode);
        Assert.Empty((await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, To = all[^1].ScheduledAt }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 本地作业在每个实例上运行。
    /// </summary>
    /// <remarks>
    /// 排除一切的日历让触发器没有下一次。
    /// </remarks>
    [Fact]
    public async Task LocalJob_RunsOnEveryInstance_AndCalendarExcludes()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddJob<RecordingJob>(new JobDefinition(new JobName("conformance.local"), JobSchedule.Every(TimeSpan.FromMilliseconds(100)), typeof(RecordingJob)) { RunOnEveryInstance = true });
            services.AddJobCalendar("conformance.never", new NeverCalendar());
            services.AddJob<RecordingJob>(new JobName("conformance.excluded"), JobSchedule.Every(TimeSpan.FromMilliseconds(50)) with { Calendar = "conformance.never" });
        }).ConfigureAwait(false);
        await log.WaitAsync(new JobName("conformance.local"), 2, WaitTimeout).ConfigureAwait(false);
        var local = await StatusAsync(host, new JobName("conformance.local")).ConfigureAwait(false);
        Assert.True(local.Job.RunOnEveryInstance);
        Assert.True(local.NextRun is not null || local.IsRunning);
        await Task.Delay(300).ConfigureAwait(false);
        Assert.DoesNotContain(log.Runs, static r => r.Run.Name == new JobName("conformance.excluded"));
        var excluded = await StatusAsync(host, new JobName("conformance.excluded")).ConfigureAwait(false);
        Assert.Equal(TriggerState.Complete, Assert.Single(excluded.Triggers).State);
        Assert.Null(excluded.NextRun);
    }

    /// <summary>
    /// 作业的状态（必须存在）。
    /// </summary>
    protected static async Task<JobStatus> StatusAsync(JobSchedulerHost host, JobName name)
    {
        ArgumentNullException.ThrowIfNull(host);
        return (await host.Scheduler.StatusAsync(name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
    }

    /// <summary>
    /// 等待条件成立。
    /// </summary>
    protected async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (!await condition().ConfigureAwait(false))
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 登记套件用的三个作业。
    /// </summary>
    private static void Register(IServiceCollection services, RecordingJobLog log)
    {
        services.AddSingleton(log);
        services.AddJob<RecordingJob>(IntervalJob, JobSchedule.Every(TimeSpan.FromMilliseconds(100)));
        services.AddJob<RecordingJob>(new JobDefinition(ManualJob, JobSchedule.Manual, typeof(RecordingJob)) { Data = new JsonObject { ["who"] = "job", ["origin"] = "job" } });
        services.AddJob<RecordingJob>(new JobDefinition(ConcurrentJob, JobSchedule.Manual, typeof(RecordingJob)) { AllowConcurrent = true });
    }

    /// <summary>
    /// 排除一切的日历。
    /// </summary>
    private sealed class NeverCalendar : IJobCalendar
    {
        /// <inheritdoc />
        public bool IsExcluded(DateTimeOffset at) => true;
    }

    /// <summary>
    /// 没有登记的作业类型。
    /// </summary>
    private sealed class UnregisteredJob : IJob
    {
        /// <inheritdoc />
        public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }
}
