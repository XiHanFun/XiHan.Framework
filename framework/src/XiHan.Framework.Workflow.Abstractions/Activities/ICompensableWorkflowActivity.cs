// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
