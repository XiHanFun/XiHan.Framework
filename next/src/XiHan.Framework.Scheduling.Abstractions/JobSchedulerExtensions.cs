// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 单条便捷方法。
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
        var results = await scheduler.TriggerAsync([name], cancellationToken).ConfigureAwait(false);
        return results[0].Outcome;
    }
}
