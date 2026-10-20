// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Assignees;

namespace XiHan.Framework.Workflow.Assignees.BuiltIn;

/// <summary>
/// 指定用户解析器（参数即受理人标识列表）
/// </summary>
public class UserAssigneeProvider : IWorkflowAssigneeProvider
{
    /// <summary>
    /// 规则类型
    /// </summary>
    public string RuleType => WorkflowAssigneeRuleTypes.User;

    /// <summary>
    /// 校验规则参数（至少一个非空标识）
    /// </summary>
    /// <param name="rule">规则</param>
    /// <returns>错误信息列表</returns>
    public IReadOnlyList<string> Validate(WorkflowAssigneeRule rule)
    {
        return rule.Values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? []
            : ["指定用户规则至少需要一个受理人标识"];
    }

    /// <summary>
    /// 解析受理人
    /// </summary>
    /// <param name="context">解析上下文</param>
    /// <returns>受理人标识列表</returns>
    public Task<IReadOnlyList<string>> ResolveAsync(WorkflowAssigneeResolveContext context)
    {
        IReadOnlyList<string> assignees = [.. context.Rule.Values.Where(value => !string.IsNullOrWhiteSpace(value))];
        return Task.FromResult(assignees);
    }
}
