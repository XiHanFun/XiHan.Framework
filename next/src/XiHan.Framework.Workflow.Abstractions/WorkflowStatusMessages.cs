// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 状态的展示名：消息键与按当前区域的文案。
/// </summary>
/// <remarks>
/// 状态在模型、查询结果、事件与回调输入里都是枚举值（程序判断用）；给人看时按消息键翻译（RFC-0010，ADR-0106），<see cref="Localize(WorkflowInstanceStatus)"/> 按当前消息作用域渲染，调用链里是调用方的区域。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowStatusMessages
{
    /// <summary>
    /// 实例状态的消息键。
    /// </summary>
    public static string KeyOf(WorkflowInstanceStatus status) => status switch
    {
        WorkflowInstanceStatus.Running => WorkflowMessages.InstanceStatusRunning,
        WorkflowInstanceStatus.Suspended => WorkflowMessages.InstanceStatusSuspended,
        WorkflowInstanceStatus.Completed => WorkflowMessages.InstanceStatusCompleted,
        WorkflowInstanceStatus.Canceled => WorkflowMessages.InstanceStatusCanceled,
        WorkflowInstanceStatus.Faulted => WorkflowMessages.InstanceStatusFaulted,
        WorkflowInstanceStatus.Terminated => WorkflowMessages.InstanceStatusTerminated,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The instance status is not defined."),
    };

    /// <summary>
    /// 节点实例状态的消息键。
    /// </summary>
    public static string KeyOf(WorkflowNodeInstanceStatus status) => status switch
    {
        WorkflowNodeInstanceStatus.Running => WorkflowMessages.NodeStatusRunning,
        WorkflowNodeInstanceStatus.Suspended => WorkflowMessages.NodeStatusSuspended,
        WorkflowNodeInstanceStatus.Completed => WorkflowMessages.NodeStatusCompleted,
        WorkflowNodeInstanceStatus.Canceled => WorkflowMessages.NodeStatusCanceled,
        WorkflowNodeInstanceStatus.Faulted => WorkflowMessages.NodeStatusFaulted,
        WorkflowNodeInstanceStatus.Compensated => WorkflowMessages.NodeStatusCompensated,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The node instance status is not defined."),
    };

    /// <summary>
    /// 定义状态的消息键。
    /// </summary>
    public static string KeyOf(WorkflowDefinitionStatus status) => status switch
    {
        WorkflowDefinitionStatus.Draft => WorkflowMessages.DefinitionStatusDraft,
        WorkflowDefinitionStatus.Published => WorkflowMessages.DefinitionStatusPublished,
        WorkflowDefinitionStatus.Disabled => WorkflowMessages.DefinitionStatusDisabled,
        WorkflowDefinitionStatus.Archived => WorkflowMessages.DefinitionStatusArchived,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The definition status is not defined."),
    };

    /// <summary>
    /// 实例状态按当前区域的展示名。
    /// </summary>
    public static string Localize(this WorkflowInstanceStatus status) => KeyOf(status).L();

    /// <summary>
    /// 节点实例状态按当前区域的展示名。
    /// </summary>
    public static string Localize(this WorkflowNodeInstanceStatus status) => KeyOf(status).L();

    /// <summary>
    /// 定义状态按当前区域的展示名。
    /// </summary>
    public static string Localize(this WorkflowDefinitionStatus status) => KeyOf(status).L();
}
