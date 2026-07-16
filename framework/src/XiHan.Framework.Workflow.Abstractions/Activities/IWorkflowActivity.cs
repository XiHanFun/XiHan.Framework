#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowActivity
// Guid:b8f05a29-1d64-4c73-9e58-a06f42d81b37
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:26:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 工作流活动（流程节点的可执行逻辑单元）
/// </summary>
/// <remarks>
/// 实现类须以 <see cref="WorkflowActivityAttribute"/> 声明活动类型编码，
/// 并注册进工作流选项的活动列表；引擎按节点的活动类型编码解析并以瞬态方式执行。
/// </remarks>
public interface IWorkflowActivity
{
    /// <summary>
    /// 执行活动
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>执行结果（完成/挂起/故障）</returns>
    Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context);
}
