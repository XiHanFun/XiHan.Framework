// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 结束活动（消耗当前令牌；所有令牌与书签均结束后实例自动完成）
/// </summary>
[WorkflowActivity(WorkflowActivityTypes.End, DisplayName = "结束", Category = "流程控制",
    OutgoingBehavior = ActivityOutgoingBehavior.None)]
public class EndActivity : WorkflowActivityBase
{
    /// <summary>
    /// 执行活动（直接完成节点，不再流转出边）
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果</returns>
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
