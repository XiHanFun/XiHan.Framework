#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FaultActivity
// Guid:71a4d0c9-6e83-4f52-b017-94d28ce65fa3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:04:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
