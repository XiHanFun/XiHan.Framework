// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 终止活动（强制结束整个实例，清理所有令牌与书签，不执行补偿）
/// </summary>
/// <remarks>
/// 节点属性：<c>Reason</c>（终止原因，支持模板）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Terminate, DisplayName = "终止", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.None)]
public class TerminateActivity : WorkflowActivityBase
{
    /// <summary>
    /// 执行活动（把实例状态置为已终止并记录终止原因）
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果</returns>
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var reason = await GetTemplatedStringAsync(context, "Reason");

        // 引擎在批次收尾阶段统一处理终止态的清理与事件发布
        context.Instance.Status = WorkflowInstanceStatus.Terminated;
        context.Instance.CancellationReason = reason ?? $"由终止节点 {context.Node.Id} 结束";

        return ActivityExecutionResult.Complete();
    }
}
