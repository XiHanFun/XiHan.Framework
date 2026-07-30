// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
