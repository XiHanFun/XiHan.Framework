// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行历史的只读视图
/// </summary>
/// <remarks>
/// <see cref="IJobScheduler.RunsAsync"/> 交给应用的一条历史；存储里的可变形态 <see cref="JobRunRecord"/> 只留给 <see cref="Storage.IJobStore"/>（ADR-0148）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRunView
{
    /// <summary>
    /// 运行 ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 作业名
    /// </summary>
    public required JobName Job { get; init; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public required JobTrigger Trigger { get; init; }

    /// <summary>
    /// 第几次尝试
    /// </summary>
    /// <remarks>
    /// 从 1 起
    /// </remarks>
    public required int Attempt { get; init; }

    /// <summary>
    /// 计划的运行时刻
    /// </summary>
    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>
    /// 开始时刻
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public required JobRunState State { get; init; }

    /// <summary>
    /// 运行所在节点
    /// </summary>
    public required string NodeId { get; init; }

    /// <summary>
    /// 触发器名
    /// </summary>
    /// <remarks>
    /// 手动触发为 <see langword="null"/>
    /// </remarks>
    public string? TriggerName { get; init; }

    /// <summary>
    /// 重试时为首次运行的 ID
    /// </summary>
    /// <remarks>
    /// 首次运行为 <see langword="null"/>
    /// </remarks>
    public string? RetryOf { get; init; }

    /// <summary>
    /// 结束时刻
    /// </summary>
    /// <remarks>
    /// 运行中为 <see langword="null"/>
    /// </remarks>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// 调用结果
    /// </summary>
    /// <remarks>
    /// 运行中为 <see langword="null"/>；失败按存储的错误码与文案还原，不再翻译
    /// </remarks>
    public Result? Outcome { get; init; }

    /// <summary>
    /// 是否已请求取消
    /// </summary>
    public bool CancelRequested { get; init; }

    /// <summary>
    /// 传给作业的数据
    /// </summary>
    /// <remarks>
    /// 快照的副本，改它不影响历史
    /// </remarks>
    public JsonObject Data { get; init; } = [];
}
