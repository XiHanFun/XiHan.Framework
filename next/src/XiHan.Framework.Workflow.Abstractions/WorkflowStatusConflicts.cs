// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 状态不允许操作的错误：每种状态一个消息键。
/// </summary>
/// <remarks>
/// 状态名不进文案（ADR-0106）：文案按状态各写一句，参数 <c>status</c> 仍是枚举名，供程序判断。
/// </remarks>
internal static class WorkflowStatusConflicts
{
    /// <summary>
    /// 实例的状态不允许该操作。
    /// </summary>
    public static Error Of(WorkflowInstance instance)
    {
        var details = MessageArguments.Of(("id", instance.Id), ("status", instance.Status.ToString()));
        return instance.Status switch
        {
            WorkflowInstanceStatus.Running => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsRunning, details),
            WorkflowInstanceStatus.Suspended => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsSuspended, details),
            WorkflowInstanceStatus.Completed => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsCompleted, details),
            WorkflowInstanceStatus.Canceled => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsCanceled, details),
            WorkflowInstanceStatus.Faulted => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsFaulted, details),
            WorkflowInstanceStatus.Terminated => Error.Conflict(WorkflowErrors.InstanceStatus, WorkflowMessages.InstanceIsTerminated, details),
            _ => throw new ArgumentOutOfRangeException(nameof(instance), instance.Status, "The instance status is not defined."),
        };
    }

    /// <summary>
    /// 定义的状态不允许该操作。
    /// </summary>
    public static Error Of(WorkflowDefinition definition)
    {
        var details = MessageArguments.Of(("id", definition.Id), ("status", definition.Status.ToString()));
        return definition.Status switch
        {
            WorkflowDefinitionStatus.Draft => Error.Conflict(WorkflowErrors.DefinitionStatus, WorkflowMessages.DefinitionIsDraft, details),
            WorkflowDefinitionStatus.Published => Error.Conflict(WorkflowErrors.DefinitionStatus, WorkflowMessages.DefinitionIsPublished, details),
            WorkflowDefinitionStatus.Disabled => Error.Conflict(WorkflowErrors.DefinitionStatus, WorkflowMessages.DefinitionIsDisabled, details),
            WorkflowDefinitionStatus.Archived => Error.Conflict(WorkflowErrors.DefinitionStatus, WorkflowMessages.DefinitionIsArchived, details),
            _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Status, "The definition status is not defined."),
        };
    }
}
