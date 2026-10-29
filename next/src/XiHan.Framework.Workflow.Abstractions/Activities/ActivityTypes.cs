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
    /// <summary>
    /// 开始。
    /// </summary>
    /// <remarks>
    /// 每个定义恰好一个。
    /// </remarks>
    public const string Start = "Start";

    /// <summary>
    /// 结束。
    /// </summary>
    /// <remarks>
    /// 可带 <c>Outcome</c>，有结果时调用结果处理器并结束实例。
    /// </remarks>
    public const string End = "End";

    /// <summary>
    /// 终止实例。
    /// </summary>
    /// <remarks>
    /// <c>Reason</c>。
    /// </remarks>
    public const string Terminate = "Terminate";

    /// <summary>
    /// 设变量。
    /// </summary>
    /// <remarks>
    /// <c>Values</c>（字面量）与 <c>Expressions</c>（表达式）。
    /// </remarks>
    public const string SetVariable = "SetVariable";

    /// <summary>
    /// 决策。
    /// </summary>
    /// <remarks>
    /// 出边 Exclusive。
    /// </remarks>
    public const string Decision = "Decision";

    /// <summary>
    /// 并行网关。
    /// </summary>
    /// <remarks>
    /// 出边 All。
    /// </remarks>
    public const string Parallel = "Parallel";

    /// <summary>
    /// 汇合。
    /// </summary>
    /// <remarks>
    /// <c>Mode</c> = <c>WaitAll</c>（默认）/ <c>WaitAny</c>。
    /// </remarks>
    public const string Join = "Join";

    /// <summary>
    /// 延时。
    /// </summary>
    /// <remarks>
    /// <c>Duration</c> 秒（数字或表达式）。
    /// </remarks>
    public const string Delay = "Delay";

    /// <summary>
    /// 用户任务。
    /// </summary>
    public const string UserTask = "UserTask";

    /// <summary>
    /// 发布自定义事件。
    /// </summary>
    /// <remarks>
    /// <c>EventName</c>、<c>Payload</c>。
    /// </remarks>
    public const string PublishEvent = "PublishEvent";

    /// <summary>
    /// 等待信号。
    /// </summary>
    /// <remarks>
    /// <c>SignalName</c>、<c>AcceptAnyCorrelation</c>。
    /// </remarks>
    public const string WaitSignal = "WaitSignal";

    /// <summary>
    /// 子流程。
    /// </summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>
    /// 对集合逐项启动子流程。
    /// </summary>
    public const string ForEach = "ForEach";

    /// <summary>
    /// 写日志。
    /// </summary>
    /// <remarks>
    /// <c>Message</c>、<c>Level</c>。
    /// </remarks>
    public const string Log = "Log";

    /// <summary>
    /// 故障。
    /// </summary>
    /// <remarks>
    /// <c>Message</c>。
    /// </remarks>
    public const string Fault = "Fault";

    /// <summary>
    /// 事件网关：等待多个信号或超时中先到的一个。
    /// </summary>
    public const string EventGateway = "EventGateway";

    /// <summary>
    /// 补偿：对已完成的节点逆序执行补偿后继续。
    /// </summary>
    public const string Compensate = "Compensate";

    /// <summary>
    /// HTTP 请求。
    /// </summary>
    public const string Http = "Http";

    /// <summary>
    /// 外部任务：由外部工作者领取并完成。
    /// </summary>
    public const string ExternalTask = "ExternalTask";
}
