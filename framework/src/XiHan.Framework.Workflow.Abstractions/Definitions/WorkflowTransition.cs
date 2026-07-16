#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowTransition
// Guid:c5d09e37-4a81-4b6f-92c3-e18f60a5d742
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:09:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 流程连线（节点间转移）
/// </summary>
public class WorkflowTransition
{
    /// <summary>
    /// 连线标识（流程定义内唯一）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 连线名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 源节点标识
    /// </summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>
    /// 目标节点标识
    /// </summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>
    /// 转移条件表达式（为空表示无条件；求值上下文为实例变量 + outcome 活动结果变量）
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// 优先级（独占网关按数值升序逐条求值，取第一条满足的连线）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 是否默认连线（独占网关所有条件连线均不满足时的兜底分支）
    /// </summary>
    public bool IsDefault { get; set; }
}
