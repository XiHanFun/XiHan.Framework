// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 触发器键：作业名 + 触发器名（触发器名与作业名同文法，作业内唯一）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct TriggerKey
{
    /// <summary>
    /// 创建；触发器名不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public TriggerKey(JobName job, string trigger)
    {
        Job = job;
        Trigger = JobName.Validate(trigger);
    }

    /// <summary>
    /// 作业名。
    /// </summary>
    public JobName Job { get; }

    /// <summary>
    /// 触发器名。
    /// </summary>
    public string Trigger { get; }

    /// <inheritdoc />
    public override string ToString() => Job.Value + "/" + Trigger;
}
