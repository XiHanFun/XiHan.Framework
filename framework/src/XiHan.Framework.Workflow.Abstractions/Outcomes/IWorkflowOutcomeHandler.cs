// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Outcomes;

/// <summary>
/// 流程结果处理器（结果结束节点执行时同步调用，用于把审批结论回写到业务）
/// </summary>
/// <remarks>
/// 声明了 <c>Outcome</c> 的结束节点执行时，引擎在结束实例之前依次调用全部已注册的处理器：
/// 任一处理器抛出异常即该结束节点故障、实例进入故障态，实例不会被标记为完成；
/// 修正原因后重试实例会重新执行结束节点并再次调用处理器，因此实现必须幂等。
/// 处理器运行在实例执行锁内，不得再同步驱动同一实例（启动/恢复/取消），否则会锁等待超时。
/// </remarks>
public interface IWorkflowOutcomeHandler
{
    /// <summary>
    /// 处理流程结果
    /// </summary>
    /// <param name="context">结果上下文</param>
    /// <returns>任务</returns>
    Task HandleAsync(WorkflowOutcomeContext context);
}
