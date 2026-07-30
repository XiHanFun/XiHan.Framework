// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 汇聚网关波次状态（按汇聚节点记录当前波次的分支到达情况）
/// </summary>
public class WorkflowJoinState
{
    /// <summary>
    /// 当前波次已到达的入边连线标识
    /// </summary>
    public HashSet<string> ArrivedTransitionIds { get; set; } = [];

    /// <summary>
    /// 当前波次是否已触发（WaitAny 模式触发后吞掉同波次后续到达）
    /// </summary>
    public bool Fired { get; set; }
}
