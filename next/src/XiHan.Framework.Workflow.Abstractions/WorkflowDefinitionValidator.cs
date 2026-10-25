// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 定义校验（RFC-0021）：结构、活动类型、转移、条件语法、汇合与用户任务的属性、指派规则、可达性。作用域服务（指派规则校验用到 Provider）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowDefinitionValidator
{
    /// <summary>
    /// 活动登记表。
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 求值器。
    /// </summary>
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 指派规则解析。
    /// </summary>
    private readonly AssigneeResolver _assignees;

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowDefinitionValidator(ActivityRegistry registry, IWorkflowExpressionEvaluator evaluator, AssigneeResolver assignees)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _assignees = assignees ?? throw new ArgumentNullException(nameof(assignees));
    }

    /// <summary>
    /// 校验；不合法返回 <see cref="WorkflowErrors.DefinitionInvalid"/>，<c>details.errors</c> 是全部问题。
    /// </summary>
    public Result Validate(WorkflowDefinition definition)
    {
        var errors = Errors(definition);
        return errors.Count == 0
            ? Result.Success()
            : Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, MessageArguments.Of(("errors", string.Join("; ", errors))));
    }

    /// <summary>
    /// 全部问题的说明（给设计器）；空表示合法。
    /// </summary>
    public IReadOnlyList<string> Errors(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.Nodes.Count == 0)
        {
            errors.Add("The definition has no nodes.");
            return errors;
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        WorkflowNode? start = null;
        foreach (var node in definition.Nodes)
        {
            if (!nodeIds.Add(node.Id))
            {
                errors.Add($"Node ID '{node.Id}' is duplicated.");
            }

            if (!_registry.Contains(node.ActivityType))
            {
                errors.Add($"Node '{node.Id}': activity type '{node.ActivityType}' is not registered.");
            }

            if (string.Equals(node.ActivityType.Value, ActivityTypes.Start, StringComparison.Ordinal))
            {
                if (start is not null)
                {
                    errors.Add("The definition has more than one Start node.");
                }

                start ??= node;
            }

            ValidateNodeProperties(node, errors);
        }

        if (start is null)
        {
            errors.Add("The definition has no Start node.");
        }

        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        var defaults = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var transition in definition.Transitions)
        {
            if (!transitionIds.Add(transition.Id))
            {
                errors.Add($"Transition ID '{transition.Id}' is duplicated.");
            }

            if (!nodeIds.Contains(transition.SourceNodeId))
            {
                errors.Add($"Transition '{transition.Id}': source node '{transition.SourceNodeId}' does not exist.");
            }

            if (!nodeIds.Contains(transition.TargetNodeId))
            {
                errors.Add($"Transition '{transition.Id}': target node '{transition.TargetNodeId}' does not exist.");
            }

            if (start is not null && string.Equals(transition.TargetNodeId, start.Id, StringComparison.Ordinal))
            {
                errors.Add($"Transition '{transition.Id}': the Start node must not have incoming transitions.");
            }

            if (transition.IsDefault)
            {
                defaults[transition.SourceNodeId] = defaults.GetValueOrDefault(transition.SourceNodeId) + 1;
            }

            if (!string.IsNullOrWhiteSpace(transition.Condition))
            {
                var syntax = _evaluator.Validate(transition.Condition);
                if (syntax.IsFailure)
                {
                    errors.Add($"Transition '{transition.Id}': {syntax.Error!.Message}");
                }
            }
        }

        foreach (var (source, count) in defaults)
        {
            if (count > 1)
            {
                errors.Add($"Node '{source}' has more than one default transition.");
            }
        }

        if (start is not null)
        {
            var graph = new WorkflowDefinitionGraph(definition);
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>([start.Id]);
            while (queue.TryDequeue(out var current))
            {
                if (!reachable.Add(current))
                {
                    continue;
                }

                foreach (var transition in graph.Outgoing(current))
                {
                    queue.Enqueue(transition.TargetNodeId);
                }
            }

            foreach (var node in definition.Nodes)
            {
                if (!reachable.Contains(node.Id))
                {
                    errors.Add($"Node '{node.Id}' is not reachable from the Start node.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// 按活动类型校验属性：汇合模式、用户任务的策略、指派来源与规则、结束结果。
    /// </summary>
    private void ValidateNodeProperties(WorkflowNode node, List<string> errors)
    {
        var properties = node.Properties;
        switch (node.ActivityType.Value)
        {
            case ActivityTypes.Join:
                if (properties.TryGetPropertyValue(JoinActivity.ModeProperty, out var mode) && mode is JsonValue modeValue)
                {
                    var text = WorkflowValues.AsString(modeValue);
                    if (!string.Equals(text, JoinActivity.WaitAll, StringComparison.OrdinalIgnoreCase) && !string.Equals(text, JoinActivity.WaitAny, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"Node '{node.Id}': join mode '{text}' is unknown.");
                    }
                }

                break;
            case ActivityTypes.End:
                if (properties.TryGetPropertyValue(EndActivity.OutcomeProperty, out var outcome) && outcome is JsonValue outcomeValue && string.IsNullOrWhiteSpace(WorkflowValues.AsString(outcomeValue)))
                {
                    errors.Add($"Node '{node.Id}': the End outcome must not be empty.");
                }

                break;
            case ActivityTypes.UserTask:
                ValidateUserTask(node, errors);
                break;
        }
    }

    /// <summary>
    /// 用户任务：至少一个指派来源，策略与空指派策略取值合法，规则类型已登记且参数合法，兜底策略要有兜底规则。
    /// </summary>
    private void ValidateUserTask(WorkflowNode node, List<string> errors)
    {
        var properties = node.Properties;
        var hasAssignees = WorkflowValues.ToStringList(properties.TryGetPropertyValue(UserTaskActivity.AssigneesProperty, out var assignees) ? assignees : null).Count > 0;
        var hasExpression = properties.TryGetPropertyValue(UserTaskActivity.AssigneesExpressionProperty, out var expression) && expression is JsonValue;
        var rules = AssigneeRule.Parse(properties.TryGetPropertyValue(UserTaskActivity.AssigneeRulesProperty, out var rulesNode) ? rulesNode : null);
        if (!rules.TryGetValue(out var parsedRules))
        {
            errors.Add($"Node '{node.Id}': {rules.Error!.Message}");
            parsedRules = [];
        }

        if (!hasAssignees && !hasExpression && parsedRules.Count == 0)
        {
            errors.Add($"Node '{node.Id}': a UserTask needs Assignees, AssigneesExpression or AssigneeRules.");
        }

        foreach (var error in _assignees.Validate(parsedRules))
        {
            errors.Add($"Node '{node.Id}': {error}");
        }

        if (properties.TryGetPropertyValue(UserTaskActivity.CompletionPolicyProperty, out var policy) && policy is not null && !WorkflowValues.TryConvert<UserTaskCompletionPolicy>(policy, out _))
        {
            errors.Add($"Node '{node.Id}': completion policy '{policy}' is unknown.");
        }

        var strategy = EmptyAssigneeStrategy.Fault;
        if (properties.TryGetPropertyValue(UserTaskActivity.EmptyAssigneeStrategyProperty, out var strategyNode) && strategyNode is not null)
        {
            if (!WorkflowValues.TryConvert(strategyNode, out strategy))
            {
                errors.Add($"Node '{node.Id}': empty assignee strategy '{strategyNode}' is unknown.");
            }
        }

        if (strategy == EmptyAssigneeStrategy.Fallback)
        {
            var fallback = AssigneeRule.Parse(properties.TryGetPropertyValue(UserTaskActivity.FallbackAssigneeRulesProperty, out var fallbackNode) ? fallbackNode : null);
            if (!fallback.TryGetValue(out var fallbackRules) || fallbackRules.Count == 0)
            {
                errors.Add($"Node '{node.Id}': the Fallback strategy needs FallbackAssigneeRules.");
            }
            else
            {
                foreach (var error in _assignees.Validate(fallbackRules))
                {
                    errors.Add($"Node '{node.Id}': {error}");
                }
            }
        }
    }
}
