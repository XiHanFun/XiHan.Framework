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
    /// <summary>
    /// 执行活动（直接完成节点，由引擎沿所有出边扇出）
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果</returns>
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
