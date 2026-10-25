// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 指派规则解析：按类型找 Provider（不区分大小写，同类型两个 Provider 是编程错误），结果裁剪、去重、保序。作用域服务。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class AssigneeResolver
{
    /// <summary>
    /// 规则类型 → Provider。
    /// </summary>
    private readonly Dictionary<string, IAssigneeProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 创建。
    /// </summary>
    public AssigneeResolver(IEnumerable<IAssigneeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.RuleType, provider))
            {
                throw new ArgumentException($"Assignee rule type '{provider.RuleType}' is registered more than once.", nameof(providers));
            }
        }
    }

    /// <summary>
    /// 已登记的规则类型。
    /// </summary>
    public IReadOnlyCollection<string> RuleTypes => _providers.Keys;

    /// <summary>
    /// 是否登记了该类型。
    /// </summary>
    public bool Supports(string ruleType) => ruleType is not null && _providers.ContainsKey(ruleType);

    /// <summary>
    /// 校验一组规则：未登记的类型与 Provider 拒绝的参数都算错误；返回错误说明（空表示合法）。
    /// </summary>
    public IReadOnlyList<string> Validate(IEnumerable<AssigneeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new List<string>();
        foreach (var rule in rules)
        {
            if (!_providers.TryGetValue(rule.Type, out var provider))
            {
                errors.Add($"Assignee rule type '{rule.Type}' is not registered.");
                continue;
            }

            var validated = provider.Validate(rule);
            if (validated.IsFailure)
            {
                errors.Add($"Assignee rule '{rule.Type}': {validated.Error!.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// 解析一组规则的指派人。
    /// </summary>
    public async ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(IReadOnlyList<AssigneeRule> rules, ActivityContext activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(activity);
        var result = new List<string>();
        foreach (var rule in rules)
        {
            if (!_providers.TryGetValue(rule.Type, out var provider))
            {
                return Error.NotFound(WorkflowErrors.TypeNotRegistered, WorkflowMessages.AssigneeRuleUnknown, MessageArguments.Of(("type", rule.Type)));
            }

            var resolved = await provider.ResolveAsync(new AssigneeResolution(rule, activity), cancellationToken).ConfigureAwait(false);
            if (!resolved.TryGetValue(out var assignees))
            {
                return resolved.Error!;
            }

            result.AddRange(assignees);
        }

        return result.Select(static assignee => assignee.Trim()).Where(static assignee => assignee.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }
}
