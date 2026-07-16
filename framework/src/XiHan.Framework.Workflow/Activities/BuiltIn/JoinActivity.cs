#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JoinActivity
// Guid:9d47c60b-e2f8-4315-a9d4-70b53c18e6f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:09:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 汇聚网关活动（等待并行分支到达后合并继续）
/// </summary>
/// <remarks>
/// 节点属性：<c>Mode</c>（<c>WaitAll</c> 等待全部入边到达（默认）；<c>WaitAny</c> 任一到达即触发，同波次后续到达吞掉）。
/// 分支到达计数由引擎按波次维护，本活动触发时直接完成。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Join, DisplayName = "汇聚网关", Category = "流程控制")]
public class JoinActivity : WorkflowActivityBase
{
    /// <inheritdoc />
    public override Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        return Task.FromResult(ActivityExecutionResult.Complete());
    }
}
