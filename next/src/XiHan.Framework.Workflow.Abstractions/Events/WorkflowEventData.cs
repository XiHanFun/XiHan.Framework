// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// <c>workflow.*</c> 消息的数据（一种形状覆盖全部主题，用不到的字段为空）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WorkflowEventData
{
    /// <summary>
    /// 实例 ID。
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// 定义 ID。
    /// </summary>
    public required string DefinitionId { get; init; }

    /// <summary>
    /// 定义名。
    /// </summary>
    public required string DefinitionName { get; init; }

    /// <summary>
    /// 定义版本。
    /// </summary>
    public int DefinitionVersion { get; init; }

    /// <summary>
    /// 实例状态。
    /// </summary>
    public WorkflowInstanceStatus Status { get; init; }

    /// <summary>
    /// 实例显示名。
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 节点 ID（节点与任务事件）。
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 节点实例 ID。
    /// </summary>
    public string? NodeInstanceId { get; init; }

    /// <summary>
    /// 任务 ID（任务事件）。
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// 指派人（任务创建、转办目标）。
    /// </summary>
    public string? AssigneeId { get; init; }

    /// <summary>
    /// 操作者（任务完成、转办）。
    /// </summary>
    public string? ActorId { get; init; }

    /// <summary>
    /// 结果（任务完成、实例结束）。
    /// </summary>
    public string? Outcome { get; init; }

    /// <summary>
    /// 原因或意见。
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 故障节点事件：是否会重试。
    /// </summary>
    public bool? WillRetry { get; init; }

    /// <summary>
    /// 故障。
    /// </summary>
    public WorkflowFault? Fault { get; init; }

    /// <summary>
    /// 关联 ID。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public string? StarterId { get; init; }

    /// <summary>
    /// 父实例 ID。
    /// </summary>
    public string? ParentInstanceId { get; init; }

    /// <summary>
    /// 租户。
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// 抄送人（任务创建）。
    /// </summary>
    public IReadOnlyList<string>? CcUserIds { get; init; }

    /// <summary>
    /// 附加载荷（自定义事件、任务表单）。
    /// </summary>
    public JsonObject? Payload { get; init; }

    /// <summary>
    /// 发生时间。
    /// </summary>
    public DateTimeOffset At { get; init; }
}
