// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 本地触发器（作业 <c>RunOnEveryInstance</c>）在本节点的推算状态。
/// </summary>
/// <remarks>
/// 不经存储抢占，每个实例按自己的时钟运行。
/// </remarks>
internal sealed class LocalTriggerState
{
    /// <summary>
    /// 创建。
    /// </summary>
    public LocalTriggerState(TriggerRecord trigger, JobRecord job, DateTimeOffset? nextFireAt)
    {
        Trigger = trigger;
        Job = job;
        NextFireAt = nextFireAt;
        FireCount = trigger.FireCount;
    }

    /// <summary>
    /// 触发器记录（最近一次刷新的快照）。
    /// </summary>
    public TriggerRecord Trigger { get; set; }

    /// <summary>
    /// 作业记录（最近一次刷新的快照）。
    /// </summary>
    public JobRecord Job { get; set; }

    /// <summary>
    /// 本节点的下一次运行时刻。
    /// </summary>
    /// <remarks>
    /// 间隔计划在运行结束前为空。
    /// </remarks>
    public DateTimeOffset? NextFireAt { get; set; }

    /// <summary>
    /// 本节点的上一次运行时刻。
    /// </summary>
    public DateTimeOffset? PreviousFireAt { get; set; }

    /// <summary>
    /// 本节点已运行次数（从存储里的次数起算）。
    /// </summary>
    public long FireCount { get; set; }

    /// <summary>
    /// 本节点进行中的运行数。
    /// </summary>
    public int Running { get; set; }
}
