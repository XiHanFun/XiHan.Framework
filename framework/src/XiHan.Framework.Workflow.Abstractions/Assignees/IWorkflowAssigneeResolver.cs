// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 受理人规则解析门面（按规则类型分派给已注册的 <see cref="IWorkflowAssigneeProvider"/>）
/// </summary>
public interface IWorkflowAssigneeResolver
{
    /// <summary>
    /// 已注册的规则类型
    /// </summary>
    IReadOnlyCollection<string> SupportedRuleTypes { get; }

    /// <summary>
    /// 规则类型是否已注册
    /// </summary>
    /// <param name="ruleType">规则类型（大小写不敏感）</param>
    /// <returns>已注册返回 true</returns>
    bool IsSupported(string ruleType);

    /// <summary>
    /// 校验一组规则（类型已注册且参数合法）
    /// </summary>
    /// <param name="rules">规则集合</param>
    /// <returns>错误信息列表（为空表示合法）</returns>
    IReadOnlyList<string> Validate(IEnumerable<WorkflowAssigneeRule> rules);

    /// <summary>
    /// 按顺序解析一组规则并合并受理人（保持首次出现顺序、去重、去空白）
    /// </summary>
    /// <param name="rules">规则集合</param>
    /// <param name="definition">流程定义</param>
    /// <param name="instance">流程实例</param>
    /// <param name="node">人工任务节点</param>
    /// <param name="variables">实例变量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受理人标识列表</returns>
    /// <exception cref="Exceptions.WorkflowException">规则类型未注册时抛出</exception>
    Task<List<string>> ResolveAsync(
        IEnumerable<WorkflowAssigneeRule> rules,
        WorkflowDefinition definition,
        WorkflowInstance instance,
        WorkflowNode node,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default);
}
