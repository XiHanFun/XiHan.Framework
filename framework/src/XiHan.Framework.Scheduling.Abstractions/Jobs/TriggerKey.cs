// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 触发器键：作业名 + 触发器名（触发器名与作业名同文法，作业内唯一）
/// </summary>
/// <remarks>
/// <see langword="default"/> 值不是合法键，读取成员抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct TriggerKey
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// 作业名是 <see langword="default"/> 或触发器名不合法时抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public TriggerKey(JobName job, string trigger)
    {
        // 作业名是 default 时在这里就拒绝，不让不合法的键流进调度器
        if (job == default)
        {
            throw new ArgumentException("The job name must not be the default value.", nameof(job));
        }

        Job = job;
        _trigger = JobName.Validate(trigger);
    }

    /// <summary>
    /// 校验过的触发器名
    /// </summary>
    /// <remarks>
    /// 只有 <see langword="default"/> 值为 <see langword="null"/>
    /// </remarks>
    private readonly string? _trigger;

    /// <summary>
    /// 作业名
    /// </summary>
    public JobName Job { get; }

    /// <summary>
    /// 触发器名
    /// </summary>
    public string Trigger => _trigger ?? throw new InvalidOperationException("The 'TriggerKey' is the default value and has no trigger; create it with the constructor.");

    /// <inheritdoc />
    public override string ToString() => _trigger is null ? string.Empty : Job.Value + "/" + _trigger;
}
