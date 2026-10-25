// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 节点的一次执行记录；重试复用同一记录并递增 <see cref="Attempt"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowNodeInstance
{
    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 所属实例 ID。
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 节点 ID。
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// 活动类型。
    /// </summary>
    public required ActivityType ActivityType { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public WorkflowNodeInstanceStatus Status { get; set; } = WorkflowNodeInstanceStatus.Running;

    /// <summary>
    /// 第几次尝试，从 1 起。
    /// </summary>
    public int Attempt { get; set; } = 1;

    /// <summary>
    /// 开始时间。
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// 结束时间。
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// 输入（恢复时的输入）。
    /// </summary>
    public JsonObject Inputs { get; set; } = [];

    /// <summary>
    /// 输出（完成时合并进实例变量；<c>outcome</c> 也记在这里）。
    /// </summary>
    public JsonObject Outputs { get; set; } = [];

    /// <summary>
    /// 活动的私有状态，跨挂起保留。
    /// </summary>
    public JsonObject State { get; set; } = [];

    /// <summary>
    /// 故障。
    /// </summary>
    public WorkflowFault? Fault { get; set; }

    /// <summary>
    /// 补偿时间。
    /// </summary>
    public DateTimeOffset? CompensatedAt { get; set; }
}
