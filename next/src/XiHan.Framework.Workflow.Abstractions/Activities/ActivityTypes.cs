// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 内置活动的类型名。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ActivityTypes
{
    /// <summary>开始；每个定义恰好一个。</summary>
    public const string Start = "Start";

    /// <summary>结束；可带 <c>Outcome</c>，有结果时调用结果处理器并结束实例。</summary>
    public const string End = "End";

    /// <summary>终止实例；<c>Reason</c>。</summary>
    public const string Terminate = "Terminate";

    /// <summary>设变量；<c>Values</c>（字面量）与 <c>Expressions</c>（表达式）。</summary>
    public const string SetVariable = "SetVariable";

    /// <summary>决策；出边 Exclusive。</summary>
    public const string Decision = "Decision";

    /// <summary>并行网关；出边 All。</summary>
    public const string Parallel = "Parallel";

    /// <summary>汇合；<c>Mode</c> = <c>WaitAll</c>（默认）/ <c>WaitAny</c>。</summary>
    public const string Join = "Join";

    /// <summary>延时；<c>Duration</c> 秒（数字或表达式）。</summary>
    public const string Delay = "Delay";

    /// <summary>用户任务。</summary>
    public const string UserTask = "UserTask";

    /// <summary>发布自定义事件；<c>EventName</c>、<c>Payload</c>。</summary>
    public const string PublishEvent = "PublishEvent";

    /// <summary>等待信号；<c>SignalName</c>、<c>AcceptAnyCorrelation</c>。</summary>
    public const string WaitSignal = "WaitSignal";

    /// <summary>子流程。</summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>对集合逐项启动子流程。</summary>
    public const string ForEach = "ForEach";

    /// <summary>写日志；<c>Message</c>、<c>Level</c>。</summary>
    public const string Log = "Log";

    /// <summary>故障；<c>Message</c>。</summary>
    public const string Fault = "Fault";
}
