// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一个作业的状态快照。
/// </summary>
/// <param name="Job">作业记录（定义的持久化形态）。</param>
/// <param name="Triggers">每个触发器的状态。</param>
/// <param name="RunningCount">运行中的数量（整个集群）。</param>
/// <param name="Last">最近一次完成的运行；没有为空。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobStatus(JobRecord Job, IReadOnlyList<JobTriggerStatus> Triggers, int RunningCount, JobOutcome? Last)
{
    /// <summary>
    /// 作业名。
    /// </summary>
    public JobName Name => Job.Name;

    /// <summary>
    /// 是否暂停。
    /// </summary>
    public bool Paused => Job.Paused;

    /// <summary>
    /// 是否有运行在进行。
    /// </summary>
    public bool IsRunning => RunningCount > 0;

    /// <summary>
    /// 最早的下一次运行时刻（等待中或已抢占的触发器里取最小）。
    /// </summary>
    /// <remarks>
    /// 没有为空。
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
