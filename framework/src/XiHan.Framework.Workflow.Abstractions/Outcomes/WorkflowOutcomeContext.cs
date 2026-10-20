// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Outcomes;

/// <summary>
/// 流程结果上下文
/// </summary>
public class WorkflowOutcomeContext
{
    /// <summary>
    /// 流程定义
    /// </summary>
    public required WorkflowDefinition Definition { get; init; }

    /// <summary>
    /// 流程实例（此时仍为运行态，处理器全部成功后才结束）
    /// </summary>
    public required WorkflowInstance Instance { get; init; }

    /// <summary>
    /// 命中的结果结束节点
    /// </summary>
    public required WorkflowNode Node { get; init; }

    /// <summary>
    /// 流程结果（结束节点声明的 Outcome，如 approved / rejected）
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// 当前实例变量（只读视图）
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Variables { get; init; }

    /// <summary>
    /// 服务提供者（当前执行作用域）
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
