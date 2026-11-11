// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业的只读视图
/// </summary>
/// <remarks>
/// 调度器交给应用的作业快照；存储里的可变形态 <see cref="Storage.JobRecord"/> 只留给 <see cref="Storage.IJobStore"/>，引擎的簿记字段（进行中的运行、代码指纹）不在这里（ADR-0148）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobView
{
    /// <summary>
    /// 作业名
    /// </summary>
    public required JobName Name { get; init; }

    /// <summary>
    /// 作业类型名
    /// </summary>
    public required string JobTypeName { get; init; }

    /// <summary>
    /// 来源
    /// </summary>
    public required JobSource Source { get; init; }

    /// <summary>
    /// 是否暂停
    /// </summary>
    public required bool Paused { get; init; }

    /// <summary>
    /// 是否允许并发运行
    /// </summary>
    public required bool AllowConcurrent { get; init; }

    /// <summary>
    /// 是否在每个实例上都运行
    /// </summary>
    public required bool RunOnEveryInstance { get; init; }

    /// <summary>
    /// 节点失联后是否恢复运行
    /// </summary>
    public required bool RequestsRecovery { get; init; }

    /// <summary>
    /// 最后修改时刻
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// 说明
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// 重试策略
    /// </summary>
    /// <remarks>
    /// 不重试为 <see langword="null"/>
    /// </remarks>
    public JobRetryPolicy? Retry { get; init; }

    /// <summary>
    /// 单次运行的超时
    /// </summary>
    /// <remarks>
    /// 不限时为 <see langword="null"/>
    /// </remarks>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// 作业数据
    /// </summary>
    /// <remarks>
    /// 快照的副本，改它不影响调度器
    /// </remarks>
    public JsonObject Data { get; init; } = [];
}
