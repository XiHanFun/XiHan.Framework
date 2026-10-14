// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 受理人解析上下文（一条规则一次解析）
/// </summary>
public class WorkflowAssigneeResolveContext
{
    /// <summary>
    /// 待解析的规则
    /// </summary>
    public required WorkflowAssigneeRule Rule { get; init; }

    /// <summary>
    /// 流程定义
    /// </summary>
    public required WorkflowDefinition Definition { get; init; }

    /// <summary>
    /// 流程实例（含发起人、业务相关性标识与租户）
    /// </summary>
    public required WorkflowInstance Instance { get; init; }

    /// <summary>
    /// 当前人工任务节点
    /// </summary>
    public required WorkflowNode Node { get; init; }

    /// <summary>
    /// 当前实例变量（只读视图）
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Variables { get; init; }

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
