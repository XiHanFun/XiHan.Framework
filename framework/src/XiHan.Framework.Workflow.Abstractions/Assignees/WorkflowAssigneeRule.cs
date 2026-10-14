// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 受理人规则（人工任务在节点执行时按规则解析出受理人）
/// </summary>
/// <remarks>
/// <see cref="Type"/> 选择受理人解析器：框架内置 <see cref="WorkflowAssigneeRuleTypes"/> 中的类型，
/// 应用可通过 <see cref="IWorkflowAssigneeProvider"/> 注册角色、部门、上级主管等自有类型。
/// <see cref="Values"/> 是该类型的参数，含义由对应解析器约定（用户标识列表、表达式、主管层级等）。
/// </remarks>
public class WorkflowAssigneeRule
{
    /// <summary>
    /// 规则类型（大小写不敏感）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 规则参数（含义由解析器约定）
    /// </summary>
    public List<string> Values { get; set; } = [];
}
