#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParallelGatewayActivity
// Guid:e63a90f5-28d1-4c47-9b06-52f84dae01c9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:08:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 并行网关活动（忽略条件沿所有出边扇出并行分支）
/// </summary>
[WorkflowActivity(WorkflowActivityTypes.Parallel, DisplayName = "并行网关", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.All)]
public class ParallelGatewayActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
