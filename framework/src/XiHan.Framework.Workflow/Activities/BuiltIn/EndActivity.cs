#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EndActivity
// Guid:f04b91d7-83c6-4e25-a1f8-60c3d97b25e4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:02:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
