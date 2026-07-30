// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 独占网关活动（条件分支：按出边优先级取第一条条件满足的分支，均不满足走默认分支）
/// </summary>
[WorkflowActivity(WorkflowActivityTypes.Decision, DisplayName = "独占网关", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.Exclusive)]
public class DecisionActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
