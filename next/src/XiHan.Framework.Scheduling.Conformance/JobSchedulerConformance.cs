// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <see cref="IJobScheduler"/> 的一致性套件。继承并实现 <see cref="StartAsync"/>：以回调登记套件的作业后启动宿主，返回被测调度器。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class JobSchedulerConformance
{
    /// <summary>
    /// 间隔计划的作业名。
    /// </summary>
    protected static JobName IntervalJob => new("conformance.interval");

    /// <summary>
    /// 手动计划的作业名。
    /// </summary>
    protected static JobName ManualJob => new("conformance.manual");

    /// <summary>
    /// 允许并发的手动作业名。
    /// </summary>
    protected static JobName ConcurrentJob => new("conformance.concurrent");

    /// <summary>
    /// 间隔计划用例等待运行的上限。
    /// </summary>
    protected virtual TimeSpan WaitTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 启动装配了被测调度器的宿主；<paramref name="configure"/> 登记套件的作业与记录，必须在宿主构建前应用。
    /// </summary>
    protected abstract Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure);

    /// <summary>
    /// 用例 1：手动触发经管道运行，记录里有运行 ID 作为关联 ID；结果与状态一致；手动计划没有下一次运行。
    /// </summary>
    [Fact]
    public async Task Trigger_RunsTheJob_AndReportsOutcomeAndStatus()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        var outcome = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.True(outcome.IsSuccess);
        var (run, correlation) = Assert.Single(log.Runs);
        Assert.Equal((ManualJob, JobTrigger.Manual), (run.Name, run.Trigger));
        Assert.Equal(run.Id, correlation);

        var status = host.Scheduler.Status(ManualJob).ValueOrThrow();
        Assert.Null(status.NextRun);
        Assert.False(status.IsRunning);
        Assert.Equal(run.Id, status.Last!.Run.Id);
        Assert.True(status.Last.Result.IsSuccess);
        Assert.True(status.Last.CompletedAt >= status.Last.StartedAt);
        Assert.Contains(host.Scheduler.Statuses(), s => s.Name == ManualJob);

        // 失败的结果原样返回并记入状态
        log.FailWith = Error.Conflict("CONFORMANCE_CONFLICT_001", "conformance.job.rejected");
        var failed = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal("CONFORMANCE_CONFLICT_001", failed.Error?.Code);
        Assert.Equal("CONFORMANCE_CONFLICT_001", host.Scheduler.Status(ManualJob).ValueOrThrow().Last!.Result.Error?.Code);
    }

    /// <summary>
    /// 用例 2：未登记的作业是 NOTFOUND；批量触发逐项返回；空列表返回空。
    /// </summary>
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
        Assert.False(host.Scheduler.Status(new JobName("conformance.missing")).HasValue);
    }

    /// <summary>
    /// 用例 3：间隔计划的作业反复按计划运行，状态里有下一次运行时刻。
    /// </summary>
    [Fact]
    public async Task IntervalSchedule_RunsRepeatedly()
    {
        var log = new RecordingJobLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        await log.WaitAsync(IntervalJob, 2, WaitTimeout).ConfigureAwait(false);
        Assert.All(log.Runs.Where(r => r.Run.Name == IntervalJob), static r => Assert.Equal(JobTrigger.Schedule, r.Run.Trigger));
        var status = host.Scheduler.Status(IntervalJob).ValueOrThrow();
        Assert.NotNull(status.Last);
        Assert.True(status.NextRun is not null || status.IsRunning);
    }

    /// <summary>
    /// 用例 4：不允许并发的作业运行中再次触发是 CONFLICT；允许并发的作业可以重叠。
    /// </summary>
    [Fact]
    public async Task ConcurrentTrigger_ConflictsUnlessAllowed()
    {
        var log = new RecordingJobLog { Delay = TimeSpan.FromMilliseconds(400) };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        var first = host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).AsTask();
        await log.WaitAsync(ManualJob, 1, WaitTimeout).ConfigureAwait(false);
        Assert.True(host.Scheduler.Status(ManualJob).ValueOrThrow().IsRunning);
        var second = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.JobRunning, second.Error?.Code);
        Assert.True((await first.ConfigureAwait(false)).IsSuccess);

        var a = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        var b = host.Scheduler.TriggerAsync(ConcurrentJob, CancellationToken.None).AsTask();
        Assert.All(await Task.WhenAll(a, b).ConfigureAwait(false), static r => Assert.True(r.IsSuccess));
        Assert.Equal(2, log.Runs.Count(r => r.Run.Name == ConcurrentJob));
    }

    /// <summary>
    /// 用例 5：作业抛出异常翻译为失败结果，不让调度器崩溃，之后仍可触发。
    /// </summary>
    [Fact]
    public async Task ThrowingJob_IsAFailureResult()
    {
        var log = new RecordingJobLog { Throw = true };
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        var thrown = await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false);
        Assert.True(thrown.IsFailure);
        Assert.False(host.Scheduler.Status(ManualJob).ValueOrThrow().IsRunning);

        log.Throw = false;
        Assert.True((await host.Scheduler.TriggerAsync(ManualJob, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 登记套件的三个作业与记录。
    /// </summary>
    private static void Register(IServiceCollection services, RecordingJobLog log)
    {
        services.AddSingleton(log);
        services.AddJob<RecordingJob>(new JobDefinition(IntervalJob, JobSchedule.Every(TimeSpan.FromMilliseconds(100)), typeof(RecordingJob)) { LeaderOnly = false });
        services.AddJob<RecordingJob>(ManualJob, JobSchedule.Manual);
        services.AddJob<RecordingJob>(new JobDefinition(ConcurrentJob, JobSchedule.Manual, typeof(RecordingJob)) { AllowConcurrent = true });
    }
}
