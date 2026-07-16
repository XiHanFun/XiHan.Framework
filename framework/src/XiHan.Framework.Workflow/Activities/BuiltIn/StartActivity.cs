#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StartActivity
// Guid:d38f60a2-5c17-4be9-9d04-71a2e58c36bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:01:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 开始活动（流程入口，直接完成并流转）
/// </summary>
[WorkflowActivity(WorkflowActivityTypes.Start, DisplayName = "开始", Category = "流程控制")]
public class StartActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
