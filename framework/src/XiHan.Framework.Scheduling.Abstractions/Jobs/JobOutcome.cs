// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一次运行的结果：起止时刻、触发来源与调用结果
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142、ADR-0148）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobOutcome
{
    /// <summary>
    /// 运行
    /// </summary>
    public required JobRun Run { get; init; }

    /// <summary>
    /// 开始时刻
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// 结束时刻
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// 调用结果
    /// </summary>
    public required Result Result { get; init; }
}
