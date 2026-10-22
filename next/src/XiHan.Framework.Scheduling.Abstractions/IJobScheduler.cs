// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 作业调度器（RFC-0013）：按计划运行登记的作业、手动触发、报告状态。手动触发不受领导租约限制。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IJobScheduler
{
    /// <summary>
    /// 立即运行并等待结果，按顺序逐项；未登记 → <see cref="SchedulingErrors.JobNotFound"/>，正在运行且不允许并发 → <see cref="SchedulingErrors.JobRunning"/>。
    /// </summary>
    ValueTask<IReadOnlyList<JobTriggerResult>> TriggerAsync(IReadOnlyList<JobName> names, CancellationToken cancellationToken);

    /// <summary>
    /// 一个作业的状态；未登记为无值。
    /// </summary>
    Maybe<JobStatus> Status(JobName name);

    /// <summary>
    /// 全部作业的状态，按名字排序。
    /// </summary>
    IReadOnlyList<JobStatus> Statuses();
}
