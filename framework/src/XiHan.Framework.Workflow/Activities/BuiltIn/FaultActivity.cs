// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 抛出故障活动（主动以业务错误故障流程）
/// </summary>
/// <remarks>
/// 节点属性：<c>Message</c>（故障信息，支持模板）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Fault, DisplayName = "抛出故障", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.None)]
public class FaultActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var message = await GetTemplatedStringAsync(context, "Message");
        return ActivityExecutionResult.Fault(message ?? $"流程在故障节点 {context.Node.Id} 主动抛出故障");
    }
}
