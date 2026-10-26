// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 工作流事件的主题（装配了 <c>messaging.queue</c> 时发布）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class WorkflowTopics
{
    /// <summary>
    /// CloudEvents 的 <c>Source</c>。
    /// </summary>
    public const string Source = "/workflow";

    /// <summary>实例启动。</summary>
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

    /// <summary>实例跳转到指定节点。</summary>
    public const string InstanceJumped = "workflow.instance.jumped";

    /// <summary>实例迁移到另一个定义版本。</summary>
    public const string InstanceMigrated = "workflow.instance.migrated";

    /// <summary>实例级超时到期。</summary>
    public const string InstanceTimedOut = "workflow.instance.timedout";

    /// <summary>节点开始执行。</summary>
    public const string NodeExecuting = "workflow.node.executing";

    /// <summary>节点执行完（完成或挂起）。</summary>
    public const string NodeExecuted = "workflow.node.executed";

    /// <summary>节点故障（<c>WillRetry</c> 表示是否会重试）。</summary>
    public const string NodeFaulted = "workflow.node.faulted";

    /// <summary>用户任务创建。</summary>
    public const string TaskCreated = "workflow.task.created";

    /// <summary>用户任务完成。</summary>
    public const string TaskCompleted = "workflow.task.completed";

    /// <summary>用户任务转办。</summary>
    public const string TaskTransferred = "workflow.task.transferred";

    /// <summary>用户任务被认领。</summary>
    public const string TaskClaimed = "workflow.task.claimed";

    /// <summary>用户任务释放认领。</summary>
    public const string TaskReleased = "workflow.task.released";

    /// <summary>用户任务退回。</summary>
    public const string TaskReturned = "workflow.task.returned";

    /// <summary>用户任务催办。</summary>
    public const string TaskUrged = "workflow.task.urged";

    /// <summary>用户任务提醒（按 <c>ReminderAfterSeconds</c> 周期发布）。</summary>
    public const string TaskReminder = "workflow.task.reminder";

    /// <summary>外部任务创建。</summary>
    public const string ExternalCreated = "workflow.external.created";

    /// <summary>外部任务完成。</summary>
    public const string ExternalCompleted = "workflow.external.completed";

    /// <summary>外部任务失败。</summary>
    public const string ExternalFailed = "workflow.external.failed";

    /// <summary>自定义事件（<c>PublishEvent</c> 活动，<c>Subject</c> = 事件名）。</summary>
    public const string Custom = "workflow.custom";
}
