// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Assignees;

/// <summary>
/// 受理人规则解析门面默认实现
/// </summary>
/// <remarks>
/// 按规则类型（大小写不敏感）分派给已注册的解析器；同一类型注册了多个解析器时构造即抛出，
/// 避免"后注册覆盖先注册"的隐式替换。替换内置类型请用 <c>Replace</c> 覆盖对应注册。
/// </remarks>
public class WorkflowAssigneeResolver : IWorkflowAssigneeResolver
{
    private readonly Dictionary<string, IWorkflowAssigneeProvider> _providers;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="providers">已注册的受理人解析器</param>
    /// <exception cref="WorkflowException">同一规则类型注册了多个解析器时抛出</exception>
    public WorkflowAssigneeResolver(IEnumerable<IWorkflowAssigneeProvider> providers)
    {
        _providers = new Dictionary<string, IWorkflowAssigneeProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.RuleType))
            {
                throw new WorkflowException($"受理人解析器 {provider.GetType().FullName} 未声明规则类型");
            }

            if (!_providers.TryAdd(provider.RuleType, provider))
            {
                throw new WorkflowException(
                    $"受理人规则类型 {provider.RuleType} 注册了多个解析器：{_providers[provider.RuleType].GetType().FullName}、{provider.GetType().FullName}");
            }
        }
    }

    /// <summary>
    /// 已注册的规则类型
    /// </summary>
    public IReadOnlyCollection<string> SupportedRuleTypes => _providers.Keys;

    /// <summary>
    /// 规则类型是否已注册
    /// </summary>
    /// <param name="ruleType">规则类型</param>
    /// <returns>已注册返回 true</returns>
    public bool IsSupported(string ruleType)
    {
        return !string.IsNullOrWhiteSpace(ruleType) && _providers.ContainsKey(ruleType);
    }

    /// <summary>
    /// 校验一组规则
    /// </summary>
    /// <param name="rules">规则集合</param>
    /// <returns>错误信息列表</returns>
    public IReadOnlyList<string> Validate(IEnumerable<WorkflowAssigneeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var errors = new List<string>();
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.Type))
            {
                errors.Add("受理人规则未配置类型");
                continue;
            }

            if (!_providers.TryGetValue(rule.Type, out var provider))
            {
                errors.Add($"受理人规则类型 {rule.Type} 未注册");
                continue;
            }

            errors.AddRange(provider.Validate(rule));
        }

        return errors;
    }

    /// <summary>
    /// 按顺序解析一组规则并合并受理人
    /// </summary>
    /// <param name="rules">规则集合</param>
    /// <param name="definition">流程定义</param>
    /// <param name="instance">流程实例</param>
    /// <param name="node">人工任务节点</param>
    /// <param name="variables">实例变量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受理人标识列表</returns>
    public async Task<List<string>> ResolveAsync(
        IEnumerable<WorkflowAssigneeRule> rules,
        WorkflowDefinition definition,
        WorkflowInstance instance,
        WorkflowNode node,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var assignees = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rule is null || string.IsNullOrWhiteSpace(rule.Type))
            {
                throw new WorkflowException($"人工任务节点 {node.Id} 存在未配置类型的受理人规则");
            }

            // fail-closed：未注册的类型说明定义与运行环境不一致，不能当作"无人命中"静默放过
            if (!_providers.TryGetValue(rule.Type, out var provider))
            {
                throw new WorkflowException($"人工任务节点 {node.Id} 的受理人规则类型 {rule.Type} 未注册");
            }

            var resolved = await provider.ResolveAsync(new WorkflowAssigneeResolveContext
            {
                Rule = rule,
                Definition = definition,
                Instance = instance,
                Node = node,
                Variables = variables,
                CancellationToken = cancellationToken
            });

            foreach (var assignee in resolved)
            {
                if (!string.IsNullOrWhiteSpace(assignee) && seen.Add(assignee.Trim()))
                {
                    assignees.Add(assignee.Trim());
                }
            }
        }

        return assignees;
    }
}
