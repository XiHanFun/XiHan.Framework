// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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
/// Provider 的测试项目继承并实现 <see cref="StartAsync"/>，装配被测 Provider，并把轮询周期设得很长（如 3600 秒），让后台循环不动作。
/// 用例由自己驱动时间：套件在回调里登记一个只由用例推进的时钟（<see cref="TimeProvider"/>），引擎、选举、作业的停留与执行时限都按它计时；用例推进时钟、经 <see cref="JobSchedulerHost.PollAsync"/> 轮询一次（等本周期起的运行连同收尾结束）再断言，不按墙钟等待，结果与机器快慢无关。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class JobSchedulerConformance
{
    /// <summary>
    /// 套件时钟的起点。
    /// </summary>
    /// <remarks>
    /// 整秒：存储往返不丢精度，断言可以按确切时刻比较。
    /// </remarks>
    private static readonly DateTimeOffset _start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 间隔作业的间隔。
    /// </summary>
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 本用例的时钟。
    /// </summary>
    /// <remarks>
    /// xUnit 为每个用例新建测试类实例，时钟随之各用各的；计时器只在推进时触发。
    /// </remarks>
    private readonly FakeTimeProvider _clock = new(_start);

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
    /// 等待作业开始运行的上限。
    /// </summary>
    /// <remarks>
    /// 只防止用例挂死：等待由作业的记录唤醒，不参与判定。
    /// </remarks>
    protected virtual TimeSpan WaitTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 启动装配了被测 Provider 的宿主。
    /// </summary>
    /// <remarks>
    /// 回调 <paramref name="configure"/> 里有套件的作业与时钟，要在 Provider 装配之前应用（<c>TestHost</c> 的服务回调即是）；<paramref name="configuration"/> 里是用例要求的引擎配置（如 misfire 阈值）。
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
        var (run, correlation, origin) = Assert.Single(log.Runs, static r => r.Run.Name == ManualJob);
        Assert.Equal((ManualJob, JobTrigger.Manual, _start), (run.Name, run.Trigger, run.ScheduledAt));
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
        // 失败的结果原样返回并记入状态与历史；推进时钟让两次运行的计划时刻不同，历史按它降序
        _clock.Advance(TimeSpan.FromSeconds(1));
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
    /// <remarks>
    /// 固定延迟：上一次完成后再等一个间隔。
    /// </remarks>
    [Fact]
    public async Task IntervalSchedule_RunsRepeatedly()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        // 首个周期把代码作业写入存储：第一次在一个间隔之后
        await host.PollAsync().ConfigureAwait(false);
        Assert.Equal(0, CountOf(log, IntervalJob));
        Assert.Equal(_start + _interval, (await StatusAsync(host, IntervalJob).ConfigureAwait(false)).NextRun);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        var runs = log.Runs.Where(static r => r.Run.Name == IntervalJob).Select(static r => r.Run).ToList();
        Assert.All(runs, static r => Assert.Equal((JobTrigger.Schedule, JobTriggerDefinition.DefaultName), (r.Trigger, r.TriggerName)));
        // 作业不耗时：完成即计划时刻，下一次在它之后一个间隔
        Assert.Equal([_start + _interval, _start + (_interval * 2)], runs.Select(static r => r.ScheduledAt));
        var status = await StatusAsync(host, IntervalJob).ConfigureAwait(false);
        Assert.NotNull(status.Last);
        Assert.False(status.IsRunning);
        Assert.Equal(_start + (_interval * 3), status.NextRun);
        var trigger = Assert.Single(status.Triggers);
        Assert.Equal((JobTriggerDefinition.DefaultName, 2L), (trigger.Name, trigger.FireCount));
        Assert.Equal(_start + (_interval * 2), trigger.PreviousFireAt);
    }

    /// <summary>
    /// 不允许并发的作业运行中再触发返回冲突。
    /// </summary>
    /// <remarks>
    /// 允许并发的可以同时运行。作业停在注入的时钟上，推进时钟才结束。
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
        _clock.Advance(log.Delay);
        Assert.True((await first.ConfigureAwait(false)).IsSuccess);
        // 两次都开始之后才推进时钟：同时在运行
        var a = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        var b = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        await log.WaitAsync(ConcurrentJob, 2, WaitTimeout).ConfigureAwait(false);
        Assert.Equal(2, (await StatusAsync(host, ConcurrentJob).ConfigureAwait(false)).RunningCount);
        _clock.Advance(log.Delay);
        Assert.All(await Task.WhenAll(a, b).ConfigureAwait(false), static r => Assert.True(r.IsSuccess));
        Assert.Equal(2, CountOf(log, ConcurrentJob));
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
        var definition = new JobDefinition(dynamic, JobSchedule.Every(_interval), typeof(RecordingJob)) { Data = new JsonObject { ["k"] = "v" } };
        Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        Assert.Equal(2, CountOf(log, dynamic));
        Assert.Equal("v", log.Runs.First(r => r.Run.Name == dynamic).Run.Data["k"]!.GetValue<string>());
        Assert.Equal(JobSource.Runtime, (await StatusAsync(host, dynamic).ConfigureAwait(false)).Job.Source);
        // 暂停：不再按计划运行，手动触发仍可用
        Assert.True((await host.Scheduler.PauseAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        var paused = await StatusAsync(host, dynamic).ConfigureAwait(false);
        Assert.True(paused.Paused);
        Assert.Equal(TriggerState.Paused, Assert.Single(paused.Triggers).State);
        await AdvanceAsync(host, _interval * 4).ConfigureAwait(false);
        Assert.Equal(2, CountOf(log, dynamic));
        Assert.True((await host.Scheduler.TriggerAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(3, CountOf(log, dynamic));
        // 恢复：暂停期间到期的那次（未超过 misfire 阈值）在下一个周期补上，之后按间隔继续
        Assert.True((await host.Scheduler.ResumeAsync(dynamic, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await host.PollAsync().ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        Assert.Equal(5, CountOf(log, dynamic));
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
                new JobTriggerDefinition("fast", JobSchedule.Every(_interval)) { Data = new JsonObject { ["t"] = "fast" }, Priority = 1 },
                new JobTriggerDefinition("once", JobSchedule.Once(_clock.GetUtcNow() + (_interval * 2))) { Data = new JsonObject { ["t"] = "once" }, Priority = 9 },
            ],
        };
        Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        // 第二个间隔时两条同时到期，各自运行
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        var once = Assert.Single(log.Runs, r => r.Run.Name == multi && r.Run.TriggerName == "once");
        Assert.Equal(("once", "job"), (once.Run.Data["t"]!.GetValue<string>(), once.Run.Data["origin"]!.GetValue<string>()));
        Assert.Equal(2, log.Runs.Count(r => r.Run.Name == multi && r.Run.TriggerName == "fast"));
        Assert.Equal("fast", log.Runs.First(r => r.Run.Name == multi && r.Run.TriggerName == "fast").Run.Data["t"]!.GetValue<string>());
        var status = await StatusAsync(host, multi).ConfigureAwait(false);
        Assert.Equal(["fast", "once"], status.Triggers.Select(static t => t.Name));
        var onceStatus = status.Triggers.Single(static t => t.Name == "once");
        Assert.Equal((TriggerState.Complete, 1L), (onceStatus.State, onceStatus.FireCount));
        // 单个触发器的暂停与恢复：暂停期间它不运行，作业本身不算暂停
        var key = new TriggerKey(multi, "fast");
        Assert.True((await host.Scheduler.PauseTriggersAsync([key], CancellationToken.None).ConfigureAwait(false))[0].IsSuccess);
        Assert.Equal(TriggerState.Paused, (await StatusAsync(host, multi).ConfigureAwait(false)).Triggers.Single(static t => t.Name == "fast").State);
        Assert.False((await StatusAsync(host, multi).ConfigureAwait(false)).Paused);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        Assert.Equal(2, log.Runs.Count(r => r.Run.Name == multi && r.Run.TriggerName == "fast"));
        Assert.True((await host.Scheduler.ResumeTriggersAsync([key], CancellationToken.None).ConfigureAwait(false))[0].IsSuccess);
        Assert.Equal(TriggerState.Waiting, (await StatusAsync(host, multi).ConfigureAwait(false)).Triggers.Single(static t => t.Name == "fast").State);
        await host.PollAsync().ConfigureAwait(false);
        Assert.Equal(3, log.Runs.Count(r => r.Run.Name == multi && r.Run.TriggerName == "fast"));
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
        var period = TimeSpan.FromMilliseconds(200);
        var runOnce = new JobName("conformance.misfire.once");
        var runAll = new JobName("conformance.misfire.all");
        var skip = new JobName("conformance.misfire.skip");
        foreach (var (name, policy) in new[] { (runOnce, MisfirePolicy.RunOnce), (runAll, MisfirePolicy.RunAll), (skip, MisfirePolicy.Skip) })
        {
            var definition = new JobDefinition(name, typeof(RecordingJob)) { Triggers = [new JobTriggerDefinition("t", JobSchedule.Every(period)) { MisfirePolicy = policy }] };
            Assert.True((await host.Scheduler.ScheduleAsync(definition, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        await AdvanceAsync(host, period).ConfigureAwait(false);
        foreach (var name in new[] { runOnce, runAll, skip })
        {
            Assert.Equal(1, CountOf(log, name));
            Assert.True((await host.Scheduler.PauseAsync(name, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        // 暂停期间下一次的到期时刻（第二个周期）过去了远超阈值的时间
        _clock.Advance(TimeSpan.FromMilliseconds(800));
        var resumedAt = _clock.GetUtcNow();
        foreach (var name in new[] { runOnce, runAll, skip })
        {
            Assert.True((await host.Scheduler.ResumeAsync(name, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }

        // Skip：恢复时丢弃错过的时刻，从现在推算
        Assert.Equal(resumedAt + period, (await StatusAsync(host, skip).ConfigureAwait(false)).NextRun);
        await host.PollAsync().ConfigureAwait(false);
        // RunOnce：补的那次按"现在"计划；RunAll：补的那次按原计划时刻（在恢复之前）；Skip 不补
        var onceRun = Assert.Single(log.Runs.Where(r => r.Run.Name == runOnce).Skip(1)).Run;
        var allRun = Assert.Single(log.Runs.Where(r => r.Run.Name == runAll).Skip(1)).Run;
        Assert.Equal(resumedAt, onceRun.ScheduledAt);
        Assert.Equal(_start + (period * 2), allRun.ScheduledAt);
        Assert.Equal(1, CountOf(log, skip));
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
        // 入队即返回：运行由之后的轮询周期抢占
        Assert.Equal(0, CountOf(log, ManualJob));
        await host.PollAsync().ConfigureAwait(false);
        var (run, _, _) = Assert.Single(log.Runs, static r => r.Run.Name == ManualJob);
        Assert.Equal((JobTrigger.Manual, "queue"), (run.Trigger, run.Data["who"]!.GetValue<string>()));
        Assert.Null(run.TriggerName);
        Assert.Empty((await StatusAsync(host, ManualJob).ConfigureAwait(false)).Triggers);
        Assert.Equal(SchedulingErrors.JobNotFound, (await host.Scheduler.EnqueueAsync(new JobName("conformance.missing"), null, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 取消运行：作业的令牌被取消，历史记为取消。
    /// </summary>
    [Fact]
    public async Task CancelRun_StopsTheJob_AndRecordsCanceled()
    {
        // 作业停在注入的时钟上，不推进就一直运行，直到被取消
        var log = new RecordingJobLog { Delay = TimeSpan.FromSeconds(5) };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var triggered = host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).AsTask();
        await log.WaitAsync(ManualJob, 1, WaitTimeout).ConfigureAwait(false);
        var running = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, States = [JobRunState.Running] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var run = Assert.Single(running);
        Assert.Equal(Assert.Single(log.Runs, static r => r.Run.Name == ManualJob).Run.Id, run.Id);
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
        // 每次触发之间推进时钟：计划时刻各不相同，降序与分页是确定的
        for (var index = 0; index < 3; index++)
        {
            Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        log.FailWith = Error.Conflict("CONFORMANCE_CONFLICT_001", "conformance.job.rejected");
        Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsFailure);
        var all = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([_start.AddSeconds(3), _start.AddSeconds(2), _start.AddSeconds(1), _start], all.Select(static run => run.ScheduledAt));
        Assert.All(all, static run => Assert.NotNull(run.CompletedAt));
        var page = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, Skip = 1, Limit = 2 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(all.Skip(1).Take(2).Select(static r => r.Id), page.Select(static r => r.Id));
        var failed = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, States = [JobRunState.Failed] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("CONFORMANCE_CONFLICT_001", Assert.Single(failed).ErrorCode);
        Assert.Empty((await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, To = all[^1].ScheduledAt }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 运行历史的分页越界以校验错误返回。
    /// </summary>
    /// <remarks>
    /// 错误码 <see cref="SchedulingErrors.RunQueryInvalid"/>，不在构造时抛出、不进存储；上下边界照常查询，计数不看分页。
    /// </remarks>
    [Fact]
    public async Task History_OutOfRangePaging_IsAValidationError()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        foreach (var (skip, limit) in new[] { (0, 0), (0, JobRunQuery.MaxLimit + 1), (-1, 10) })
        {
            var rejected = await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, Skip = skip, Limit = limit }, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(SchedulingErrors.RunQueryInvalid, rejected.Error?.Code);
            Assert.Equal<object?>(limit, rejected.Error!.Details!["limit"]);
            Assert.Equal<object?>(JobRunQuery.MaxLimit, rejected.Error!.Details!["max"]);
        }

        Assert.Single((await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Single((await host.Scheduler.RunsAsync(new JobRunQuery { Job = ManualJob, Limit = JobRunQuery.MaxLimit }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(1, (await host.Scheduler.CountRunsAsync(new JobRunQuery { Job = ManualJob, Skip = -1, Limit = 0 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
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
        var local = new JobName("conformance.local");
        var excluded = new JobName("conformance.excluded");
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddJob<RecordingJob>(new JobDefinition(local, JobSchedule.Every(_interval), typeof(RecordingJob)) { RunOnEveryInstance = true });
            services.AddJobCalendar("conformance.never", new NeverCalendar());
            services.AddJob<RecordingJob>(excluded, JobSchedule.Every(TimeSpan.FromMilliseconds(50)) with { Calendar = "conformance.never" });
        }).ConfigureAwait(false);
        // 首个周期同步代码作业并建立本实例的本地触发器
        await host.PollAsync().ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        await AdvanceAsync(host, _interval).ConfigureAwait(false);
        Assert.Equal(2, CountOf(log, local));
        var status = await StatusAsync(host, local).ConfigureAwait(false);
        Assert.True(status.Job.RunOnEveryInstance);
        Assert.Equal(_start + (_interval * 3), status.NextRun);
        await AdvanceAsync(host, _interval * 3).ConfigureAwait(false);
        Assert.Equal(0, CountOf(log, excluded));
        var never = await StatusAsync(host, excluded).ConfigureAwait(false);
        Assert.Equal(TriggerState.Complete, Assert.Single(never.Triggers).State);
        Assert.Null(never.NextRun);
    }

    /// <summary>
    /// 失败的运行按重试策略再跑，成功即止、次数用尽即止。
    /// </summary>
    /// <remarks>
    /// 重试的触发来源、第几次尝试与首次运行 ID；手动触发拿到第一次的结果；每个实例各自运行的作业在本实例重试（ADR-0060）。重试在失败时刻加上等待时间到期。
    /// </remarks>
    [Fact]
    public async Task Retry_FailedRuns_AreRetried_UntilSuccessOrExhausted()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var delay = TimeSpan.FromMilliseconds(100);
        var flaky = new JobName("conformance.retry.flaky");
        var hopeless = new JobName("conformance.retry.hopeless");
        var local = new JobName("conformance.retry.local");
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(flaky, typeof(RecordingJob)) { Retry = JobRetryPolicy.Fixed(3, delay), Data = new JsonObject { ["failBeforeAttempt"] = 3 } }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(hopeless, typeof(RecordingJob)) { Retry = JobRetryPolicy.Exponential(1, delay), Data = new JsonObject { ["failBeforeAttempt"] = 99 } }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(local, typeof(RecordingJob)) { RunOnEveryInstance = true, Retry = JobRetryPolicy.Fixed(2, delay), Data = new JsonObject { ["failBeforeAttempt"] = 2 } }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        // 手动触发拿到第一次的结果；之后每过一个等待时间重试一次，第三次成功
        Assert.Equal("CONFORMANCE_CONFLICT_002", (await host.Scheduler.TriggerAsync(flaky, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        await AdvanceAsync(host, delay).ConfigureAwait(false);
        await AdvanceAsync(host, delay).ConfigureAwait(false);
        var runs = await AttemptsAsync(host, flaky).ConfigureAwait(false);
        Assert.Equal([1, 2, 3], runs.Select(static run => run.Attempt));
        Assert.Equal([JobRunState.Failed, JobRunState.Failed, JobRunState.Succeeded], runs.Select(static run => run.State));
        Assert.Equal([JobTrigger.Manual, JobTrigger.Retry, JobTrigger.Retry], runs.Select(static run => run.Trigger));
        Assert.Equal([_start, _start + delay, _start + (delay * 2)], runs.Select(static run => run.ScheduledAt));
        Assert.Equal([null, runs[0].Id, runs[0].Id], runs.Select(static run => run.RetryOf));
        Assert.All(runs.Skip(1), run => Assert.Equal(runs[0].Data.ToJsonString(), run.Data.ToJsonString()));

        // 次数用尽：失败一次、重试一次就不再跑
        Assert.True((await host.Scheduler.EnqueueAsync(hopeless, null, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        await host.PollAsync().ConfigureAwait(false);
        await AdvanceAsync(host, delay).ConfigureAwait(false);
        await AdvanceAsync(host, delay * 5).ConfigureAwait(false);
        var exhausted = await AttemptsAsync(host, hopeless).ConfigureAwait(false);
        Assert.Equal([(1, JobTrigger.Manual, JobRunState.Failed), (2, JobTrigger.Retry, JobRunState.Failed)], exhausted.Select(static run => (run.Attempt, run.Trigger, run.State)));

        // 每个实例各自运行的作业：在本实例重试
        Assert.Equal("CONFORMANCE_CONFLICT_002", (await host.Scheduler.TriggerAsync(local, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        await AdvanceAsync(host, delay).ConfigureAwait(false);
        var localRuns = await AttemptsAsync(host, local).ConfigureAwait(false);
        Assert.Equal([(1, JobRunState.Failed), (2, JobRunState.Succeeded)], localRuns.Select(static run => (run.Attempt, run.State)));
        Assert.Equal((JobTrigger.Retry, localRuns[0].Id), (localRuns[1].Trigger, localRuns[1].RetryOf));
    }

    /// <summary>
    /// 超过执行时限的运行记为超时失败并重试，被取消的运行不重试。
    /// </summary>
    /// <remarks>
    /// 执行时限与作业的停留都按注入的时钟：作业停着，推进时钟越过时限即超时。
    /// </remarks>
    [Fact]
    public async Task Timeout_FailsAndRetries_ButCanceledRunsAreNotRetried()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var limit = TimeSpan.FromMilliseconds(200);
        var retryDelay = TimeSpan.FromMilliseconds(50);
        var slow = new JobName("conformance.timeout.slow");
        var cancelable = new JobName("conformance.timeout.cancelable");
        var stalling = new JsonObject { ["delayMilliseconds"] = 10_000 };
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(slow, typeof(RecordingJob)) { Timeout = limit, Retry = JobRetryPolicy.Fixed(1, retryDelay), Data = stalling }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(cancelable, typeof(RecordingJob)) { Retry = JobRetryPolicy.Fixed(3, retryDelay), Data = stalling }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var triggered = host.Scheduler.TriggerAsync(slow, CancellationToken.None).AsTask();
        await log.WaitAsync(slow, 1, WaitTimeout).ConfigureAwait(false);
        _clock.Advance(limit);
        Assert.Equal(SchedulingErrors.RunTimedOut, (await triggered.ConfigureAwait(false)).Error?.Code);
        // 重试在等待时间之后到期，同样停住、同样超时：先发起轮询，等作业开始再推进时钟
        _clock.Advance(retryDelay);
        var polling = host.PollAsync().AsTask();
        await log.WaitAsync(slow, 2, WaitTimeout).ConfigureAwait(false);
        _clock.Advance(limit);
        await polling.ConfigureAwait(false);
        var timedOut = await AttemptsAsync(host, slow).ConfigureAwait(false);
        Assert.Equal([(1, JobRunState.Failed, SchedulingErrors.RunTimedOut), (2, JobRunState.Failed, SchedulingErrors.RunTimedOut)], timedOut.Select(static run => (run.Attempt, run.State, run.ErrorCode)));

        var canceled = host.Scheduler.TriggerAsync(cancelable, CancellationToken.None).AsTask();
        await log.WaitAsync(cancelable, 1, WaitTimeout).ConfigureAwait(false);
        var running = Assert.Single((await host.Scheduler.RunsAsync(new JobRunQuery { Job = cancelable, States = [JobRunState.Running] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.True((await host.Scheduler.CancelRunAsync(running.Id, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(SchedulingErrors.RunCanceled, (await canceled.ConfigureAwait(false)).Error?.Code);
        // 越过重试间隔再轮询：被取消的运行没有重试
        await AdvanceAsync(host, retryDelay).ConfigureAwait(false);
        Assert.Equal(JobRunState.Canceled, Assert.Single(await AttemptsAsync(host, cancelable).ConfigureAwait(false)).State);
    }

    /// <summary>
    /// 重试策略与执行时限在状态里，运行时可改，可恢复为代码定义。
    /// </summary>
    [Fact]
    public async Task RetryAndTimeout_AreInTheStatus_OverriddenAndRestored()
    {
        var log = new RecordingJobLog();
        var coded = new JobName("conformance.retry.coded");
        var codeRetry = JobRetryPolicy.Fixed(2, TimeSpan.FromSeconds(1));
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddJob<RecordingJob>(new JobDefinition(coded, JobSchedule.Manual, typeof(RecordingJob)) { Retry = codeRetry, Timeout = TimeSpan.FromSeconds(30) });
        }).ConfigureAwait(false);
        // 尚未写入存储的代码作业按代码登记报告状态
        var status = await StatusAsync(host, coded).ConfigureAwait(false);
        Assert.Equal((codeRetry, TimeSpan.FromSeconds(30), JobSource.Code), (status.Job.Retry, status.Job.Timeout, status.Job.Source));

        var runtimeRetry = JobRetryPolicy.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
        Assert.True((await host.Scheduler.ScheduleAsync(new JobDefinition(coded, typeof(RecordingJob)) { Retry = runtimeRetry }, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        status = await StatusAsync(host, coded).ConfigureAwait(false);
        Assert.Equal((runtimeRetry, null, JobSource.Overridden), (status.Job.Retry, status.Job.Timeout, status.Job.Source));

        Assert.True((await host.Scheduler.RestoreAsync([coded], CancellationToken.None).ConfigureAwait(false))[0].IsSuccess);
        status = await StatusAsync(host, coded).ConfigureAwait(false);
        Assert.Equal((codeRetry, TimeSpan.FromSeconds(30), JobSource.Code), (status.Job.Retry, status.Job.Timeout, status.Job.Source));
    }

    /// <summary>
    /// 一个作业的全部运行，按尝试次数排序。
    /// </summary>
    protected static async Task<IReadOnlyList<JobRunRecord>> AttemptsAsync(JobSchedulerHost host, JobName name)
    {
        ArgumentNullException.ThrowIfNull(host);
        var runs = (await host.Scheduler.RunsAsync(new JobRunQuery { Job = name }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        return [.. runs.OrderBy(static run => run.Attempt)];
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
    /// 某作业已记录的运行次数。
    /// </summary>
    private static int CountOf(RecordingJobLog log, JobName name) => log.Runs.Count(r => r.Run.Name == name);

    /// <summary>
    /// 推进时钟，再轮询一次（等本周期起的运行连同收尾结束）。
    /// </summary>
    private async Task AdvanceAsync(JobSchedulerHost host, TimeSpan by)
    {
        _clock.Advance(by);
        await host.PollAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 登记套件的时钟与三个作业。
    /// </summary>
    /// <remarks>
    /// 时钟登记在 Provider 装配之前，内核只在没有时才补系统时钟。
    /// </remarks>
    private void Register(IServiceCollection services, RecordingJobLog log)
    {
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton(log);
        services.AddJob<RecordingJob>(IntervalJob, JobSchedule.Every(_interval));
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
