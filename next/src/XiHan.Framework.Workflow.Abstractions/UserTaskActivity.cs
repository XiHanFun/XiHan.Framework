// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 用户任务：解析指派人（<c>Assignees</c>、<c>AssigneesExpression</c>、<c>AssigneeRules</c> 合并去重，<c>ExcludeStarter</c>），
/// 按 <c>CompletionPolicy</c>（Any / All / Sequential）给每个指派人一个 <c>UserTask</c> 书签（载荷 <c>title</c>、<c>formData</c>、<c>ccUserIds</c>），
/// 没有指派人时按 <c>EmptyAssigneeStrategy</c> 处理；完成时校验操作者与 <c>AllowedOutcomes</c>，非 <c>approved</c> 的结果立即完成节点；节点超时以 <c>timeout</c> 完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class UserTaskActivity : IResumableActivity
{
    /// <summary>属性：指派人列表。</summary>
    public const string AssigneesProperty = "Assignees";

    /// <summary>属性：指派人表达式（列表或逗号分隔字符串）。</summary>
    public const string AssigneesExpressionProperty = "AssigneesExpression";

    /// <summary>属性：指派规则（<c>[{ type, values }]</c>）。</summary>
    public const string AssigneeRulesProperty = "AssigneeRules";

    /// <summary>属性：排除发起人。</summary>
    public const string ExcludeStarterProperty = "ExcludeStarter";

    /// <summary>属性：没有指派人时的策略。</summary>
    public const string EmptyAssigneeStrategyProperty = "EmptyAssigneeStrategy";

    /// <summary>属性：兜底指派规则。</summary>
    public const string FallbackAssigneeRulesProperty = "FallbackAssigneeRules";

    /// <summary>属性：完成策略。</summary>
    public const string CompletionPolicyProperty = "CompletionPolicy";

    /// <summary>属性：标题模板。</summary>
    public const string TitleProperty = "Title";

    /// <summary>属性：表单数据。</summary>
    public const string FormDataProperty = "FormData";

    /// <summary>属性：抄送人。</summary>
    public const string CcUserIdsProperty = "CcUserIds";

    /// <summary>属性：抄送规则。</summary>
    public const string CcRulesProperty = "CcRules";

    /// <summary>属性：允许的结果；缺省 <c>approved</c> / <c>rejected</c>。</summary>
    public const string AllowedOutcomesProperty = "AllowedOutcomes";

    /// <inheritdoc />
    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resolver = context.Services.GetRequiredService<AssigneeResolver>();
        var resolved = await ResolveAssigneesAsync(context, resolver, AssigneesProperty, AssigneesExpressionProperty, AssigneeRulesProperty, cancellationToken).ConfigureAwait(false);
        if (!resolved.TryGetValue(out var assignees))
        {
            return resolved.Error!;
        }

        if (context.Property(ExcludeStarterProperty, false) && context.Instance.StarterId is { } starter)
        {
            assignees = assignees.Where(assignee => !string.Equals(assignee, starter, StringComparison.Ordinal)).ToList();
        }

        if (assignees.Count == 0)
        {
            var strategy = context.Property(EmptyAssigneeStrategyProperty, EmptyAssigneeStrategy.Fault);
            if (context.Property(EmptyAssigneeStrategyProperty) is not null && !WorkflowValues.TryConvert<EmptyAssigneeStrategy>(context.Property(EmptyAssigneeStrategyProperty), out _))
            {
                return ActivityContext.PropertyInvalid(EmptyAssigneeStrategyProperty, "unknown strategy");
            }

            switch (strategy)
            {
                case EmptyAssigneeStrategy.AutoApprove:
                    return ActivityOutcome.Complete(label: UserTaskOutcomes.Approved);
                case EmptyAssigneeStrategy.AutoReject:
                    return ActivityOutcome.Complete(label: UserTaskOutcomes.Rejected);
                case EmptyAssigneeStrategy.Fallback:
                    var fallback = await ResolveAssigneesAsync(context, resolver, null, null, FallbackAssigneeRulesProperty, cancellationToken).ConfigureAwait(false);
                    if (!fallback.TryGetValue(out assignees))
                    {
                        return fallback.Error!;
                    }

                    break;
            }

            if (assignees.Count == 0)
            {
                return Error.Validation(WorkflowErrors.InputInvalid, WorkflowMessages.TaskNoAssignee, MessageArguments.Of(("node", context.Node.Id)));
            }
        }

        if (context.Property(CompletionPolicyProperty) is not null && !WorkflowValues.TryConvert<UserTaskCompletionPolicy>(context.Property(CompletionPolicyProperty), out _))
        {
            return ActivityContext.PropertyInvalid(CompletionPolicyProperty, "unknown policy");
        }

        var policy = context.Property(CompletionPolicyProperty, UserTaskCompletionPolicy.Any);
        var title = context.RenderProperty(TitleProperty, context.Node.Title);
        if (!title.TryGetValue(out var titleText))
        {
            return title.Error!;
        }

        var cc = await ResolveAssigneesAsync(context, resolver, CcUserIdsProperty, null, CcRulesProperty, cancellationToken).ConfigureAwait(false);
        if (!cc.TryGetValue(out var ccUserIds))
        {
            return cc.Error!;
        }

        var state = context.State;
        state[UserTaskPayloadKeys.Assignees] = Array(assignees);
        state[UserTaskPayloadKeys.Approved] = new JsonArray();
        state[UserTaskPayloadKeys.Policy] = policy.ToString();
        state[UserTaskPayloadKeys.History] = new JsonArray();
        state[UserTaskPayloadKeys.Title] = titleText;
        state[UserTaskPayloadKeys.FormData] = (context.Property(FormDataProperty) as JsonObject)?.DeepClone();
        state[UserTaskPayloadKeys.CcUserIds] = Array(ccUserIds);

        var targets = policy == UserTaskCompletionPolicy.Sequential ? [assignees[0]] : assignees;
        return ActivityOutcome.Suspend(targets.Select(assignee => Bookmark(context, assignee)).ToList());
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = context.State;
        var history = (state[UserTaskPayloadKeys.History] as JsonArray)?.DeepClone() as JsonArray ?? [];
        if (context.IsTimeout)
        {
            history.Add((JsonNode?)new JsonObject { [UserTaskPayloadKeys.Outcome] = UserTaskOutcomes.Timeout, ["at"] = context.Clock.GetUtcNow() });
            state[UserTaskPayloadKeys.History] = history;
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(label: UserTaskOutcomes.Timeout)));
        }

        var inputs = context.Inputs;
        var actor = Text(inputs, UserTaskPayloadKeys.ActorId);
        var outcome = Text(inputs, UserTaskPayloadKeys.Outcome);
        var allowed = WorkflowValues.ToStringList(context.Property(AllowedOutcomesProperty));
        if (allowed.Count == 0)
        {
            allowed = [UserTaskOutcomes.Approved, UserTaskOutcomes.Rejected];
        }

        // 结果缺失或不在允许范围：重建同一个书签（任务服务已先校验，这里是最后一道闸）
        if (string.IsNullOrWhiteSpace(outcome) || !allowed.Contains(outcome, StringComparer.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(new BookmarkRequest
            {
                Kind = BookmarkKinds.UserTask,
                Key = context.Bookmark.Key,
                Payload = WorkflowJson.CloneObject(context.Bookmark.Payload),
                CorrelationId = context.Bookmark.CorrelationId,
            })));
        }

        history.Add((JsonNode?)new JsonObject
        {
            [UserTaskPayloadKeys.ActorId] = actor,
            [UserTaskPayloadKeys.Outcome] = outcome,
            [UserTaskPayloadKeys.Comment] = Text(inputs, UserTaskPayloadKeys.Comment),
            ["at"] = context.Clock.GetUtcNow(),
        });
        state[UserTaskPayloadKeys.History] = history;

        var outputs = new JsonObject();
        foreach (var (key, value) in inputs)
        {
            if (key is not (UserTaskPayloadKeys.ActorId or UserTaskPayloadKeys.Outcome or UserTaskPayloadKeys.Comment))
            {
                outputs[key] = value?.DeepClone();
            }
        }

        if (!string.Equals(outcome, UserTaskOutcomes.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, outcome.ToLowerInvariant())));
        }

        var assignees = WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Assignees]);
        var approved = WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Approved]).ToList();
        if (!string.IsNullOrWhiteSpace(actor) && !approved.Contains(actor, StringComparer.Ordinal))
        {
            approved.Add(actor);
        }

        state[UserTaskPayloadKeys.Approved] = Array(approved);
        var policy = WorkflowValues.ConvertOr(state[UserTaskPayloadKeys.Policy], UserTaskCompletionPolicy.Any);
        switch (policy)
        {
            case UserTaskCompletionPolicy.All:
                return ValueTask.FromResult(Result.Success(approved.Count >= assignees.Count
                    ? ActivityOutcome.Complete(outputs, UserTaskOutcomes.Approved)
                    : ActivityOutcome.SuspendWithOutputs(outputs)));
            case UserTaskCompletionPolicy.Sequential:
                var next = approved.Count;
                return ValueTask.FromResult(Result.Success(next < assignees.Count
                    ? ActivityOutcome.SuspendWithOutputs(outputs, Bookmark(context, assignees[next]))
                    : ActivityOutcome.Complete(outputs, UserTaskOutcomes.Approved)));
            default:
                return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, UserTaskOutcomes.Approved)));
        }
    }

    /// <summary>
    /// 给一个指派人的书签：载荷取节点状态里的标题、表单与抄送。
    /// </summary>
    internal static BookmarkRequest Bookmark(ActivityContext context, string assignee)
    {
        var state = context.State;
        var payload = new JsonObject
        {
            [UserTaskPayloadKeys.Title] = state[UserTaskPayloadKeys.Title]?.DeepClone(),
            [UserTaskPayloadKeys.FormData] = state[UserTaskPayloadKeys.FormData]?.DeepClone(),
            [UserTaskPayloadKeys.CcUserIds] = state[UserTaskPayloadKeys.CcUserIds]?.DeepClone(),
        };
        return new BookmarkRequest { Kind = BookmarkKinds.UserTask, Key = assignee, Payload = payload, CorrelationId = context.Instance.CorrelationId };
    }

    /// <summary>
    /// 合并三个来源的指派人：列表属性、表达式属性、规则属性；裁剪、去重、保序。
    /// </summary>
    private static async ValueTask<Result<IReadOnlyList<string>>> ResolveAssigneesAsync(ActivityContext context, AssigneeResolver resolver, string? listProperty, string? expressionProperty, string? rulesProperty, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        if (listProperty is not null)
        {
            result.AddRange(WorkflowValues.ToStringList(context.Property(listProperty)));
        }

        if (expressionProperty is not null && context.Property(expressionProperty) is not null)
        {
            var evaluated = context.EvaluateProperty(expressionProperty);
            if (!evaluated.TryGetValue(out var value))
            {
                return evaluated.Error!;
            }

            result.AddRange(WorkflowValues.ToStringList(value));
        }

        if (rulesProperty is not null)
        {
            var parsed = AssigneeRule.Parse(context.Property(rulesProperty));
            if (!parsed.TryGetValue(out var rules))
            {
                return parsed.Error!;
            }

            if (rules.Count > 0)
            {
                var resolved = await resolver.ResolveAsync(rules, context, cancellationToken).ConfigureAwait(false);
                if (!resolved.TryGetValue(out var fromRules))
                {
                    return resolved.Error!;
                }

                result.AddRange(fromRules);
            }
        }

        return result.Select(static assignee => assignee.Trim()).Where(static assignee => assignee.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 字符串数组。
    /// </summary>
    private static JsonArray Array(IEnumerable<string> values) => new(values.Select(static value => (JsonNode?)JsonValue.Create(value)).ToArray());

    /// <summary>
    /// 读字符串输入。
    /// </summary>
    private static string Text(JsonObject inputs, string key) => inputs.TryGetPropertyValue(key, out var value) && value is JsonValue scalar ? WorkflowValues.AsString(scalar) : string.Empty;
}
