// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Events;

/// <summary>
/// 调度事件的数据（CloudEvents 的 <c>Data</c>，JSON）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SchedulingEventData
{
    /// <summary>
    /// 作业名。
    /// </summary>
    public required string Job { get; init; }

    /// <summary>
    /// 触发器名。
    /// </summary>
    public string? TriggerName { get; init; }

    /// <summary>
    /// 运行 ID。
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// 触发来源。
    /// </summary>
    public JobTrigger? Trigger { get; init; }

    /// <summary>
    /// 运行状态。
    /// </summary>
    public JobRunState? State { get; init; }

    /// <summary>
    /// 计划时刻。
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; init; }

    /// <summary>
    /// 开始时刻。
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// 结束时刻。
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// 错误码。
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 第几次尝试。
    /// </summary>
    /// <remarks>
    /// 运行事件才有。
    /// </remarks>
    public int? Attempt { get; init; }

    /// <summary>
    /// 重试时为首次运行的 ID。
    /// </summary>
    public string? RetryOf { get; init; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 事件时刻。
    /// </summary>
    public DateTimeOffset At { get; init; }
}
