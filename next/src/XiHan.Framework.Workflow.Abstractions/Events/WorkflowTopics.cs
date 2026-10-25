// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 工作流事件的消息主题；信封 <c>Source</c> 为 <see cref="Source"/>，数据为 <see cref="WorkflowEventData"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowTopics
{
    /// <summary>信封来源。</summary>
    public const string Source = "/workflow";

    /// <summary>实例开始。</summary>
    public const string InstanceStarted = "workflow.instance.started";

    /// <summary>实例完成。</summary>
    public const string InstanceCompleted = "workflow.instance.completed";

    /// <summary>实例故障。</summary>
    public const string InstanceFaulted = "workflow.instance.faulted";

    /// <summary>实例取消。</summary>
    public const string InstanceCanceled = "workflow.instance.canceled";

    /// <summary>实例终止。</summary>
    public const string InstanceTerminated = "workflow.instance.terminated";

    /// <summary>实例挂起。</summary>
    public const string InstanceSuspended = "workflow.instance.suspended";

    /// <summary>实例恢复（含重试）。</summary>
    public const string InstanceResumed = "workflow.instance.resumed";

    /// <summary>节点开始执行。</summary>
    public const string NodeExecuting = "workflow.node.executing";

    /// <summary>节点完成或挂起。</summary>
    public const string NodeExecuted = "workflow.node.executed";

    /// <summary>节点故障（<c>WillRetry</c>）。</summary>
    public const string NodeFaulted = "workflow.node.faulted";

    /// <summary>用户任务创建。</summary>
    public const string TaskCreated = "workflow.task.created";

    /// <summary>用户任务完成。</summary>
    public const string TaskCompleted = "workflow.task.completed";

    /// <summary>用户任务转办。</summary>
    public const string TaskTransferred = "workflow.task.transferred";

    /// <summary>自定义事件；信封 <c>Subject</c> 是事件名。</summary>
    public const string Custom = "workflow.custom";
}
