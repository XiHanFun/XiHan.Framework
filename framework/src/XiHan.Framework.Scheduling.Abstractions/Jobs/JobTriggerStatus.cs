// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一个触发器的状态
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142、ADR-0148）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobTriggerStatus
{
    /// <summary>
    /// 触发器名
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 计划
    /// </summary>
    public required JobSchedule Schedule { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public required TriggerState State { get; init; }

    /// <summary>
    /// 下一次运行时刻
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? NextFireAt { get; init; }

    /// <summary>
    /// 上一次运行时刻
    /// </summary>
    /// <remarks>
    /// 没运行过为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? PreviousFireAt { get; init; }

    /// <summary>
    /// 已运行次数
    /// </summary>
    public required long FireCount { get; init; }

    /// <summary>
    /// 状态为 <see cref="TriggerState.Error"/> 时的原因
    /// </summary>
    /// <remarks>
    /// 其他状态为 <see langword="null"/>
    /// </remarks>
    public string? Error { get; init; }
}
