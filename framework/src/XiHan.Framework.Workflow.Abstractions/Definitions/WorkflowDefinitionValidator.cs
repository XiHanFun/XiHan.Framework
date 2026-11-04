// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Validation;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Expressions;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Definitions;

/// <summary>
/// 定义校验（RFC-0021）
/// </summary>
/// <remarks>
/// 结构、活动类型、转移、条件语法、汇合与用户任务的属性、指派规则、可达性。作用域服务（指派规则校验用到 Provider）。每条问题是一条 <see cref="SchemaViolation"/>：位置是定义 JSON 里的路径（<c>$.nodes[2].properties.Cron</c>、<c>$.transitions[0].condition</c>，定义级的问题是 <c>$.nodes</c>），文案是消息键与参数，宿主按调用方的区域翻译（RFC-0010，ADR-0106）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowDefinitionValidator
{
    /// <summary>
    /// 活动登记表
    /// </summary>
    private readonly ActivityRegistry _registry;

    /// <summary>
    /// 求值器
    /// </summary>
    private readonly IWorkflowExpressionEvaluator _evaluator;

    /// <summary>
    /// 指派规则解析
    /// </summary>
    private readonly AssigneeResolver _assignees;

    /// <summary>
    /// 创建
    /// </summary>
    public WorkflowDefinitionValidator(ActivityRegistry registry, IWorkflowExpressionEvaluator evaluator, AssigneeResolver assignees)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _assignees = assignees ?? throw new ArgumentNullException(nameof(assignees));
    }

    /// <summary>
    /// 校验
    /// </summary>
    /// <remarks>
    /// 不合法返回 <see cref="WorkflowErrors.DefinitionInvalid"/>（消息键 <see cref="WorkflowMessages.DefinitionInvalid"/>，参数 <c>count</c>），<c>details.errors</c> 是全部问题（<see cref="SchemaViolation"/> 的列表，宿主写成 Problem Details 的字段级 <c>errors</c>）
    /// </remarks>
    public Result Validate(WorkflowDefinition definition)
    {
        var issues = Diagnose(definition);
        return issues.Count == 0
            ? Result.Success()
            : Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionInvalid, MessageArguments.Of(("count", issues.Count), ("errors", issues)));
    }

    /// <summary>
    /// 全部问题（给设计器）
    /// </summary>
    /// <remarks>
    /// 空表示合法。每条带定义 JSON 里的路径、消息键与参数；<see cref="SchemaViolation.Reason"/> 是按进程默认区域渲染的文本（日志用），展示用消息键与参数按调用方的区域渲染
    /// </remarks>
    public IReadOnlyList<SchemaViolation> Diagnose(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<SchemaViolation>();
        if (definition.Nodes.Count == 0)
        {
            Add(issues, "$.nodes", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionNoNodes));
            return issues;
        }

        // 节点 ID → 第一次出现的下标：按 ID 报的问题（默认出边重复、不可达）落到那个节点上
        var nodeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        WorkflowNode? start = null;
        for (var i = 0; i < definition.Nodes.Count; i++)
        {
            var node = definition.Nodes[i];
            if (!nodeIndex.TryAdd(node.Id, i))
            {
                Add(issues, NodePath(i), Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionNodeDuplicate, MessageArguments.Of(("node", node.Id))));
            }

            if (!_registry.Contains(node.ActivityType))
            {
                Add(issues, NodePath(i) + ".activityType", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionActivityUnknown, MessageArguments.Of(("node", node.Id), ("type", node.ActivityType.Value))));
            }

            if (string.Equals(node.ActivityType.Value, ActivityTypes.Start, StringComparison.Ordinal))
            {
                if (start is not null)
                {
                    Add(issues, NodePath(i), Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionStartDuplicate, MessageArguments.Of(("node", node.Id))));
                }

                start ??= node;
            }

            ValidateNodeProperties(node, i, issues);
            foreach (var (variable, expression) in node.OutputMappings ?? new Dictionary<string, string>(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(variable))
                {
                    Add(issues, NodePath(i) + ".outputMappings", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionMappingVariableEmpty, MessageArguments.Of(("node", node.Id))));
                }

                var syntax = _evaluator.Validate(expression);
                if (syntax.IsFailure)
                {
                    Add(issues, NodePath(i) + ".outputMappings" + Member(variable), syntax.Error!);
                }
            }
        }

        if (start is null)
        {
            Add(issues, "$.nodes", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionStartMissing));
        }

        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        var defaults = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < definition.Transitions.Count; i++)
        {
            var transition = definition.Transitions[i];
            if (!transitionIds.Add(transition.Id))
            {
                Add(issues, TransitionPath(i), Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionTransitionDuplicate, MessageArguments.Of(("transition", transition.Id))));
            }

            if (!nodeIndex.ContainsKey(transition.SourceNodeId))
            {
                Add(issues, TransitionPath(i) + ".sourceNodeId", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionTransitionSourceMissing, MessageArguments.Of(("transition", transition.Id), ("node", transition.SourceNodeId))));
            }

            if (!nodeIndex.ContainsKey(transition.TargetNodeId))
            {
                Add(issues, TransitionPath(i) + ".targetNodeId", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionTransitionTargetMissing, MessageArguments.Of(("transition", transition.Id), ("node", transition.TargetNodeId))));
            }

            if (start is not null && string.Equals(transition.TargetNodeId, start.Id, StringComparison.Ordinal))
            {
                Add(issues, TransitionPath(i) + ".targetNodeId", Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionStartIncoming, MessageArguments.Of(("transition", transition.Id), ("node", start.Id))));
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
                    Add(issues, TransitionPath(i) + ".condition", syntax.Error!);
                }
            }
        }

        foreach (var (source, count) in defaults)
        {
            if (count > 1)
            {
                // 源节点不存在时已报过源节点缺失，这里落到定义的转移列表上
                var path = nodeIndex.TryGetValue(source, out var index) ? NodePath(index) : "$.transitions";
                Add(issues, path, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionDefaultDuplicate, MessageArguments.Of(("node", source))));
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

            for (var i = 0; i < definition.Nodes.Count; i++)
            {
                if (!reachable.Contains(definition.Nodes[i].Id))
                {
                    Add(issues, NodePath(i), Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionNodeUnreachable, MessageArguments.Of(("node", definition.Nodes[i].Id))));
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// 按活动类型校验属性：汇合模式、用户任务的策略、指派来源与规则、结束结果
    /// </summary>
    private void ValidateNodeProperties(WorkflowNode node, int index, List<SchemaViolation> issues)
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
                        Add(issues, NodePath(index, JoinActivity.ModeProperty), ActivityFailures.UnknownValue(JoinActivity.ModeProperty, text));
                    }
                }

                break;
            case ActivityTypes.End:
                if (properties.TryGetPropertyValue(EndActivity.OutcomeProperty, out var outcome) && outcome is JsonValue outcomeValue && string.IsNullOrWhiteSpace(WorkflowValues.AsString(outcomeValue)))
                {
                    Add(issues, NodePath(index, EndActivity.OutcomeProperty), ActivityFailures.Empty(EndActivity.OutcomeProperty));
                }

                break;
            case ActivityTypes.UserTask:
                ValidateUserTask(node, index, issues);
                break;
            case ActivityTypes.Start:
                if (properties.TryGetPropertyValue(StartActivity.CronProperty, out var cron) && cron is JsonValue cronValue && !CronExpression.TryParse(WorkflowValues.AsString(cronValue), out _))
                {
                    Add(issues, NodePath(index, StartActivity.CronProperty), ActivityFailures.NotCron(StartActivity.CronProperty, WorkflowValues.AsString(cronValue)));
                }

                if (properties.TryGetPropertyValue(StartActivity.TimeZoneProperty, out var zone) && zone is JsonValue zoneValue && !TimeZoneInfo.TryFindSystemTimeZoneById(WorkflowValues.AsString(zoneValue), out _))
                {
                    Add(issues, NodePath(index, StartActivity.TimeZoneProperty), ActivityFailures.NotTimeZone(StartActivity.TimeZoneProperty, WorkflowValues.AsString(zoneValue)));
                }

                break;
            case ActivityTypes.EventGateway:
                if (WorkflowValues.ToStringList(properties.TryGetPropertyValue(EventGatewayActivity.SignalsProperty, out var signals) ? signals : null).Count == 0)
                {
                    Add(issues, NodePath(index, EventGatewayActivity.SignalsProperty), ActivityFailures.NoSignal(EventGatewayActivity.SignalsProperty));
                }

                break;
            case ActivityTypes.Http:
                if (!properties.TryGetPropertyValue(HttpActivity.UrlProperty, out var url) || url is not JsonValue urlValue || string.IsNullOrWhiteSpace(WorkflowValues.AsString(urlValue)))
                {
                    Add(issues, NodePath(index, HttpActivity.UrlProperty), ActivityFailures.Empty(HttpActivity.UrlProperty));
                }

                if (properties.TryGetPropertyValue(HttpActivity.MethodProperty, out var method) && method is JsonValue methodValue && !HttpActivity.IsValidMethod(WorkflowValues.AsString(methodValue)))
                {
                    Add(issues, NodePath(index, HttpActivity.MethodProperty), ActivityFailures.NotHttpMethod(HttpActivity.MethodProperty, WorkflowValues.AsString(methodValue)));
                }

                break;
            case ActivityTypes.ExternalTask:
                if (!properties.TryGetPropertyValue(ExternalTaskActivity.TopicProperty, out var topic) || topic is not JsonValue topicValue || string.IsNullOrWhiteSpace(WorkflowValues.AsString(topicValue)))
                {
                    Add(issues, NodePath(index, ExternalTaskActivity.TopicProperty), ActivityFailures.Empty(ExternalTaskActivity.TopicProperty));
                }

                break;
        }
    }

    /// <summary>
    /// 用户任务
    /// </summary>
    /// <remarks>
    /// 至少一个指派来源，策略与空指派策略取值合法，规则类型已登记且参数合法，兜底策略要有兜底规则
    /// </remarks>
    private void ValidateUserTask(WorkflowNode node, int index, List<SchemaViolation> issues)
    {
        var properties = node.Properties;
        var hasAssignees = WorkflowValues.ToStringList(properties.TryGetPropertyValue(UserTaskActivity.AssigneesProperty, out var assignees) ? assignees : null).Count > 0;
        var hasExpression = properties.TryGetPropertyValue(UserTaskActivity.AssigneesExpressionProperty, out var expression) && expression is JsonValue;
        var rulesPath = NodePath(index, UserTaskActivity.AssigneeRulesProperty);
        var rules = AssigneeRule.Parse(properties.TryGetPropertyValue(UserTaskActivity.AssigneeRulesProperty, out var rulesNode) ? rulesNode : null);
        if (!rules.TryGetValue(out var parsedRules))
        {
            Add(issues, rulesPath, rules.Error!);
            parsedRules = [];
        }

        if (!hasAssignees && !hasExpression && parsedRules.Count == 0)
        {
            Add(issues, NodePath(index), Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionUserTaskNoAssignees, MessageArguments.Of(("node", node.Id))));
        }

        foreach (var error in _assignees.Validate(parsedRules))
        {
            Add(issues, rulesPath, error);
        }

        if (properties.TryGetPropertyValue(UserTaskActivity.CompletionPolicyProperty, out var policy) && policy is not null && !WorkflowValues.TryConvert<UserTaskCompletionPolicy>(policy, out _))
        {
            Add(issues, NodePath(index, UserTaskActivity.CompletionPolicyProperty), ActivityFailures.UnknownValue(UserTaskActivity.CompletionPolicyProperty, policy));
        }

        var strategy = EmptyAssigneeStrategy.Fault;
        if (properties.TryGetPropertyValue(UserTaskActivity.EmptyAssigneeStrategyProperty, out var strategyNode) && strategyNode is not null)
        {
            if (!WorkflowValues.TryConvert(strategyNode, out strategy))
            {
                Add(issues, NodePath(index, UserTaskActivity.EmptyAssigneeStrategyProperty), ActivityFailures.UnknownValue(UserTaskActivity.EmptyAssigneeStrategyProperty, strategyNode));
            }
        }

        if (strategy == EmptyAssigneeStrategy.Fallback)
        {
            var fallbackPath = NodePath(index, UserTaskActivity.FallbackAssigneeRulesProperty);
            var fallback = AssigneeRule.Parse(properties.TryGetPropertyValue(UserTaskActivity.FallbackAssigneeRulesProperty, out var fallbackNode) ? fallbackNode : null);
            if (!fallback.TryGetValue(out var fallbackRules) || fallbackRules.Count == 0)
            {
                Add(issues, fallbackPath, Error.Validation(WorkflowErrors.DefinitionInvalid, WorkflowMessages.DefinitionFallbackRulesMissing, MessageArguments.Of(("node", node.Id))));
            }
            else
            {
                foreach (var error in _assignees.Validate(fallbackRules))
                {
                    Add(issues, fallbackPath, error);
                }
            }
        }

        if (properties.TryGetPropertyValue(UserTaskActivity.ApprovalThresholdProperty, out var threshold) && threshold is not null && !UserTaskActivity.TryParseApprovalThreshold(threshold, out _, out _))
        {
            Add(issues, NodePath(index, UserTaskActivity.ApprovalThresholdProperty), ActivityFailures.NotThreshold(UserTaskActivity.ApprovalThresholdProperty));
        }

        if (properties.TryGetPropertyValue(UserTaskActivity.PriorityProperty, out var priority) && priority is not null && !UserTaskActivity.TryParsePriority(priority, out _))
        {
            Add(issues, NodePath(index, UserTaskActivity.PriorityProperty), ActivityFailures.NotInteger(UserTaskActivity.PriorityProperty));
        }

        foreach (var secondsProperty in new[] { UserTaskActivity.DueAfterSecondsProperty, UserTaskActivity.ReminderAfterSecondsProperty })
        {
            if (properties.TryGetPropertyValue(secondsProperty, out var seconds) && seconds is not null && !UserTaskActivity.TryParseSeconds(seconds, out _))
            {
                Add(issues, NodePath(index, secondsProperty), ActivityFailures.NotSeconds(secondsProperty));
            }
        }
    }

    /// <summary>
    /// 记一条问题：错误的消息键与参数原样带上，原因文本是错误按进程默认区域渲染的消息
    /// </summary>
    private static void Add(List<SchemaViolation> issues, string path, Error error)
        => issues.Add(new SchemaViolation(path, error.Message, error.MessageKey, error.Details));

    /// <summary>
    /// 第 <paramref name="index"/> 个节点的路径
    /// </summary>
    private static string NodePath(int index) => $"$.nodes[{index}]";

    /// <summary>
    /// 第 <paramref name="index"/> 个节点某个属性的路径
    /// </summary>
    private static string NodePath(int index, string property) => NodePath(index) + ".properties" + Member(property);

    /// <summary>
    /// 第 <paramref name="index"/> 条转移的路径
    /// </summary>
    private static string TransitionPath(int index) => $"$.transitions[{index}]";

    /// <summary>
    /// 路径里的成员段
    /// </summary>
    /// <remarks>
    /// 只含字母、数字、下划线的写成 <c>.名字</c>，否则 <c>['名字']</c>（同 JSON Schema 校验的路径写法）
    /// </remarks>
    private static string Member(string name)
        => name.Length > 0 && name.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_') ? "." + name : $"['{name.Replace("'", "\\'", StringComparison.Ordinal)}']";
}
