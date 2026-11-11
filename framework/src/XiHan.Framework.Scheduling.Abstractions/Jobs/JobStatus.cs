// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一个作业的状态快照
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）；作业是只读视图，不是存储记录（ADR-0148）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobStatus
{
    /// <summary>
    /// 作业
    /// </summary>
    public required JobView Job { get; init; }

    /// <summary>
    /// 每个触发器的状态，按触发器名的序数升序
    /// </summary>
    public required IReadOnlyList<JobTriggerStatus> Triggers { get; init; }

    /// <summary>
    /// 运行中的数量（整个集群）
    /// </summary>
    public required int RunningCount { get; init; }

    /// <summary>
    /// 最近一次完成的运行
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>
    /// </remarks>
    public JobOutcome? Last { get; init; }

    /// <summary>
    /// 作业名
    /// </summary>
    public JobName Name => Job.Name;

    /// <summary>
    /// 是否暂停
    /// </summary>
    public bool Paused => Job.Paused;

    /// <summary>
    /// 是否有运行在进行
    /// </summary>
    public bool IsRunning => RunningCount > 0;

    /// <summary>
    /// 最早的下一次运行时刻（等待中或已抢占的触发器里取最小）
    /// </summary>
    /// <remarks>
    /// 没有为空
    /// </remarks>
    public DateTimeOffset? NextRun
    {
        get
        {
            DateTimeOffset? next = null;
            foreach (var trigger in Triggers)
            {
                if (trigger.State is TriggerState.Waiting or TriggerState.Acquired or TriggerState.Blocked && trigger.NextFireAt is { } fire && (next is null || fire < next))
                {
                    next = fire;
                }
            }

            return next;
        }
    }
}
