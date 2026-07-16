#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TerminateActivity
// Guid:29c85f13-d4a0-4b76-8e92-135f60da84c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:03:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var reason = await GetTemplatedStringAsync(context, "Reason");

        // 引擎在批次收尾阶段统一处理终止态的清理与事件发布
        context.Instance.Status = WorkflowInstanceStatus.Terminated;
        context.Instance.CancellationReason = reason ?? $"由终止节点 {context.Node.Id} 结束";

        return ActivityExecutionResult.Complete();
    }
}
