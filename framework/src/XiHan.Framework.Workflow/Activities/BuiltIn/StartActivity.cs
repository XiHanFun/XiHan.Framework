// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 开始活动（流程入口，直接完成并流转）
/// </summary>
[WorkflowActivity(WorkflowActivityTypes.Start, DisplayName = "开始", Category = "流程控制")]
public class StartActivity : WorkflowActivityBase
{
    /// <summary>
    /// 执行活动（直接完成节点并流转出边）
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果</returns>
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
