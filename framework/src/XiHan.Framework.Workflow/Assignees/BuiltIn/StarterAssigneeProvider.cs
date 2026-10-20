// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Assignees;

namespace XiHan.Framework.Workflow.Assignees.BuiltIn;

/// <summary>
/// 流程发起人解析器（常用于"退回发起人修改"节点）
/// </summary>
public class StarterAssigneeProvider : IWorkflowAssigneeProvider
{
    /// <summary>
    /// 规则类型
    /// </summary>
    public string RuleType => WorkflowAssigneeRuleTypes.Starter;

    /// <summary>
    /// 校验规则参数（无参数）
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>错误信息列表</returns>
    public IReadOnlyList<string> Validate(WorkflowAssigneeRule rule)
    {
        return rule.Values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? ["发起人规则不接受参数"]
            : [];
    }

    /// <summary>
    /// 解析受理人（实例未记录发起人时为空）
    /// </summary>
    /// <param name="context">解析上下文</param>
    /// <returns>受理人标识列表</returns>
    public Task<IReadOnlyList<string>> ResolveAsync(WorkflowAssigneeResolveContext context)
    {
        IReadOnlyList<string> assignees = string.IsNullOrWhiteSpace(context.Instance.StarterId)
            ? []
            : [context.Instance.StarterId];
        return Task.FromResult(assignees);
    }
}
