// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 定义的一次执行；状态全部在存储里，引擎在实例锁内修改。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowInstance
{
    /// <summary>
    /// 实例 ID。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 定义 ID。
    /// </summary>
    public required string DefinitionId { get; set; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public required WorkflowName DefinitionName { get; set; }

    /// <summary>
    /// 定义版本。
    /// </summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>
    /// 显示名。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    /// <summary>
    /// 实例变量。
    /// </summary>
    public JsonObject Variables { get; set; } = [];

    /// <summary>
    /// 汇合节点的到达记录，键为汇合节点 ID。
    /// </summary>
    public Dictionary<string, WorkflowJoinState> JoinStates { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 业务关联 ID（如单据 ID）。
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; set; }

    /// <summary>
    /// 父实例 ID（子流程）。
    /// </summary>
    public string? ParentInstanceId { get; set; }

    /// <summary>
    /// 父实例里启动本实例的节点实例 ID。
    /// </summary>
    public string? ParentNodeInstanceId { get; set; }

    /// <summary>
    /// 子流程深度，顶层为 0。
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 开始执行时间。
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 结束时间（到达终态）。
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// 故障。
    /// </summary>
    public WorkflowFault? Fault { get; set; }

    /// <summary>
    /// 故障节点 ID。
    /// </summary>
    public string? FaultNodeId { get; set; }

    /// <summary>
    /// 故障节点实例 ID；引擎级故障为 <see langword="null"/>（不可重试）。
    /// </summary>
    public string? FaultNodeInstanceId { get; set; }

    /// <summary>
    /// 取消或终止的原因。
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// 结束节点声明的结果。
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// 是否已到终态。
    /// </summary>
    public bool IsFinal => Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Canceled or WorkflowInstanceStatus.Faulted or WorkflowInstanceStatus.Terminated;

    /// <summary>
    /// 显示名或定义名。
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? DefinitionName.Value : DisplayName;
}
