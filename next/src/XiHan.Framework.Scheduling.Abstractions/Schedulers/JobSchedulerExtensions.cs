// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// <see cref="IJobScheduler"/> 的单项便利写法：单项是批量的特例。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class JobSchedulerExtensions
{
    /// <summary>
    /// 立即运行一个作业并等待结果。
    /// </summary>
    public static async ValueTask<Result> TriggerAsync(this IJobScheduler scheduler, JobName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.TriggerAsync([new JobTriggerRequest(name)], cancellationToken).ConfigureAwait(false);
        return results[0].Outcome;
    }

    /// <summary>
    /// 带数据立即运行一个作业并等待结果。
    /// </summary>
    public static async ValueTask<Result> TriggerAsync(this IJobScheduler scheduler, JobName name, JsonObject? data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.TriggerAsync([new JobTriggerRequest(name, data)], cancellationToken).ConfigureAwait(false);
        return results[0].Outcome;
    }

    /// <summary>
    /// 按名字批量立即运行并等待结果。
    /// </summary>
    public static ValueTask<IReadOnlyList<JobTriggerResult>> TriggerAsync(this IJobScheduler scheduler, IReadOnlyList<JobName> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(names);
        return scheduler.TriggerAsync(names.Select(static name => new JobTriggerRequest(name)).ToList(), cancellationToken);
    }

    /// <summary>
    /// 登记一个作业。
    /// </summary>
    public static async ValueTask<Result> ScheduleAsync(this IJobScheduler scheduler, JobDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.ScheduleAsync([definition], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 删除一个作业。
    /// </summary>
    public static async ValueTask<Result> UnscheduleAsync(this IJobScheduler scheduler, JobName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.UnscheduleAsync([name], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 暂停一个作业。
    /// </summary>
    public static async ValueTask<Result> PauseAsync(this IJobScheduler scheduler, JobName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.PauseAsync([name], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 恢复一个作业。
    /// </summary>
    public static async ValueTask<Result> ResumeAsync(this IJobScheduler scheduler, JobName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.ResumeAsync([name], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 入队一次运行。
    /// </summary>
    public static async ValueTask<Result<string>> EnqueueAsync(this IJobScheduler scheduler, JobName name, JsonObject? data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.EnqueueAsync([new JobTriggerRequest(name, data)], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 请求取消一次运行。
    /// </summary>
    public static async ValueTask<Result> CancelRunAsync(this IJobScheduler scheduler, string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        var results = await scheduler.CancelRunsAsync([runId], cancellationToken).ConfigureAwait(false);
        return results[0];
    }
}
