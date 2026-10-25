// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一次运行：运行 ID（也是调用的关联 ID）、作业名、计划的时刻与触发来源。
/// </summary>
/// <param name="Id">运行 ID。</param>
/// <param name="Name">作业名。</param>
/// <param name="ScheduledAt">计划运行的时刻（手动触发为触发时刻）。</param>
/// <param name="Trigger">触发来源。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRun(string Id, JobName Name, DateTimeOffset ScheduledAt, JobTrigger Trigger)
{
    /// <summary>
    /// 新建一次运行，ID 为随机值。
    /// </summary>
    public static JobRun Create(JobName name, DateTimeOffset scheduledAt, JobTrigger trigger) => new(Guid.NewGuid().ToString("N"), name, scheduledAt, trigger);
}
