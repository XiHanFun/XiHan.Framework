#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICompensableWorkflowActivity
// Guid:6e30a9d8-74f2-4b5c-91e6-c8250d4f7ab1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:28:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 可补偿的工作流活动（实例取消时按执行逆序执行补偿逻辑）
/// </summary>
public interface ICompensableWorkflowActivity : IWorkflowActivity
{
    /// <summary>
    /// 补偿活动（撤销已完成节点的业务影响；补偿异常仅记录日志，不中断补偿链）
    /// </summary>
    /// <param name="context">执行上下文（节点实例为被补偿的历史记录）</param>
    /// <returns>任务</returns>
    Task CompensateAsync(ActivityExecutionContext context);
}
