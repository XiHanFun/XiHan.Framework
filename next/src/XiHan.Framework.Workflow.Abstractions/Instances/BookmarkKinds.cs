// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 内置的书签种类。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BookmarkKinds
{
    /// <summary>用户任务：<c>Key</c> = 指派人。</summary>
    public const string UserTask = "UserTask";

    /// <summary>定时：<c>DueAt</c> 到期由轮询恢复。</summary>
    public const string Timer = "Timer";

    /// <summary>信号：<c>Key</c> = 信号名，<c>CorrelationId</c> 为空接受任意关联。</summary>
    public const string Signal = "Signal";

    /// <summary>子流程回调：<c>Key</c> = 父节点实例 ID。</summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>重试：<c>DueAt</c> 到期从故障节点重试。</summary>
    public const string Retry = "Retry";

    /// <summary>节点超时：<c>DueAt</c> 到期以超时恢复节点。</summary>
    public const string NodeTimeout = "NodeTimeout";

    /// <summary>外部任务：<c>Key</c> = 主题，由外部工作者领取并完成。</summary>
    public const string External = "External";

    /// <summary>用户任务提醒：<c>DueAt</c> 到期发布提醒事件并再次排期；由引擎处理，不经活动。</summary>
    public const string Reminder = "Reminder";

    /// <summary>实例级超时：<c>DueAt</c> 到期按定义的超时动作处理实例；<c>NodeInstanceId</c> = 实例 ID。</summary>
    public const string InstanceTimeout = "InstanceTimeout";

    /// <summary>定义级定时启动：<c>InstanceId</c> = 定义 ID，到期启动一个实例并推算下一次。</summary>
    public const string DefinitionTimer = "DefinitionTimer";
}
