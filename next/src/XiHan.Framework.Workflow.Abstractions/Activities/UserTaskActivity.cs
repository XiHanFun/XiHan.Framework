// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 用户任务。
/// </summary>
/// <remarks>
/// 解析指派人（<c>Assignees</c>、<c>AssigneesExpression</c>、<c>AssigneeRules</c> 合并去重，<c>ExcludeStarter</c>），按 <c>CompletionPolicy</c>（Any / All / Sequential）给每个指派人一个 <c>UserTask</c> 书签（载荷 <c>title</c>、<c>formData</c>、<c>formKey</c>、<c>priority</c>、<c>dueAt</c>、<c>ccUserIds</c>），没有指派人时按 <c>EmptyAssigneeStrategy</c> 处理；<c>ReminderAfterSeconds</c> 另插一个 <c>Reminder</c> 书签由引擎周期发布提醒。完成时校验操作者与 <c>AllowedOutcomes</c>：<c>All</c> 按 <c>ApprovalThreshold</c>（人数或百分比，缺省全部）判定通过或拒绝，其他策略下非 <c>approved</c> 的结果立即完成节点；节点超时以 <c>timeout</c> 完成。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class UserTaskActivity : IResumableActivity
{
    /// <summary>
    /// 属性：指派人列表。
    /// </summary>
    public const string AssigneesProperty = "Assignees";

    /// <summary>
    /// 属性：指派人表达式（列表或逗号分隔字符串）。
    /// </summary>
    public const string AssigneesExpressionProperty = "AssigneesExpression";

    /// <summary>
    /// 属性：指派规则（<c>[{ type, values }]</c>）。
    /// </summary>
    public const string AssigneeRulesProperty = "AssigneeRules";

    /// <summary>
    /// 属性：排除发起人。
    /// </summary>
    public const string ExcludeStarterProperty = "ExcludeStarter";

    /// <summary>
    /// 属性：没有指派人时的策略。
    /// </summary>
    public const string EmptyAssigneeStrategyProperty = "EmptyAssigneeStrategy";

    /// <summary>
    /// 属性：兜底指派规则。
    /// </summary>
    public const string FallbackAssigneeRulesProperty = "FallbackAssigneeRules";

    /// <summary>
    /// 属性：完成策略。
    /// </summary>
    public const string CompletionPolicyProperty = "CompletionPolicy";

    /// <summary>
    /// 属性。
    /// </summary>
    /// <remarks>
    /// <c>All</c> 策略的通过阈值：正整数（人数）或 <c>"60%"</c>（百分比，按指派人数向上取整）；缺省全部指派人。
    /// </remarks>
    public const string ApprovalThresholdProperty = "ApprovalThreshold";

    /// <summary>
    /// 属性：标题模板。
    /// </summary>
    public const string TitleProperty = "Title";

    /// <summary>
    /// 属性：表单数据。
    /// </summary>
    public const string FormDataProperty = "FormData";

    /// <summary>
    /// 属性：表单键（模板）。
    /// </summary>
    public const string FormKeyProperty = "FormKey";

    /// <summary>
    /// 属性：优先级（整数）。
    /// </summary>
    public const string PriorityProperty = "Priority";

    /// <summary>
    /// 属性：截止时长（秒，相对挂起时刻）。
    /// </summary>
    public const string DueAfterSecondsProperty = "DueAfterSeconds";

    /// <summary>
    /// 属性。
    /// </summary>
    /// <remarks>
    /// 截止时刻（ISO-8601 字符串，或求值为时刻的表达式）；优先于 <see cref="DueAfterSecondsProperty"/>。
    /// </remarks>
    public const string DueAtProperty = "DueAt";

    /// <summary>
    /// 属性：提醒间隔秒数：每隔该时长发布一次 <c>workflow.task.reminder</c>，直至节点结束。
    /// </summary>
    public const string ReminderAfterSecondsProperty = "ReminderAfterSeconds";

    /// <summary>
    /// 属性：抄送人。
    /// </summary>
    public const string CcUserIdsProperty = "CcUserIds";

    /// <summary>
    /// 属性：抄送规则。
    /// </summary>
    public const string CcRulesProperty = "CcRules";

    /// <summary>
    /// 属性。
    /// </summary>
    /// <remarks>
    /// 允许的结果；缺省 <c>approved</c> / <c>rejected</c>。
    /// </remarks>
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

        if (context.Property(ApprovalThresholdProperty) is { } thresholdNode && !TryParseApprovalThreshold(thresholdNode, out _, out _))
        {
            return ActivityContext.PropertyInvalid(ApprovalThresholdProperty, "must be a positive integer or a percentage like '60%'");
        }

        var policy = context.Property(CompletionPolicyProperty, UserTaskCompletionPolicy.Any);
        var title = context.RenderProperty(TitleProperty, context.Node.Title);
        if (!title.TryGetValue(out var titleText))
        {
            return title.Error!;
        }

        var formKey = context.RenderProperty(FormKeyProperty);
        if (!formKey.TryGetValue(out var formKeyText))
        {
            return formKey.Error!;
        }

        int? priority = null;
        if (context.Property(PriorityProperty) is { } priorityNode)
        {
            if (!TryParsePriority(priorityNode, out var priorityValue))
            {
                return ActivityContext.PropertyInvalid(PriorityProperty, "must be an integer");
            }

            priority = priorityValue;
        }

        if (DueAt(context, out var dueAt) is { } dueError)
        {
            return dueError;
        }

        double? reminder = null;
        if (context.Property(ReminderAfterSecondsProperty) is { } reminderNode)
        {
            if (!TryParseSeconds(reminderNode, out var interval))
            {
                return ActivityContext.PropertyInvalid(ReminderAfterSecondsProperty, "must be a positive number of seconds");
            }

            reminder = interval;
        }

        var cc = await ResolveAssigneesAsync(context, resolver, CcUserIdsProperty, null, CcRulesProperty, cancellationToken).ConfigureAwait(false);
        if (!cc.TryGetValue(out var ccUserIds))
        {
            return cc.Error!;
        }

        var state = context.State;
        state[UserTaskPayloadKeys.Assignees] = Array(assignees);
        state[UserTaskPayloadKeys.Approved] = new JsonArray();
        state[UserTaskPayloadKeys.Rejected] = new JsonArray();
        state[UserTaskPayloadKeys.ClaimedBy] = null;
        state[UserTaskPayloadKeys.Policy] = policy.ToString();
        state[UserTaskPayloadKeys.History] = new JsonArray();
        state[UserTaskPayloadKeys.Title] = titleText;
        state[UserTaskPayloadKeys.FormData] = (context.Property(FormDataProperty) as JsonObject)?.DeepClone();
        state[UserTaskPayloadKeys.FormKey] = string.IsNullOrEmpty(formKeyText) ? null : formKeyText;
        state[UserTaskPayloadKeys.Priority] = priority is { } p ? JsonValue.Create(p) : null;
        state[UserTaskPayloadKeys.DueAt] = dueAt is { } at ? at.ToString("O", CultureInfo.InvariantCulture) : null;
        state[UserTaskPayloadKeys.CcUserIds] = Array(ccUserIds);

        var targets = policy == UserTaskCompletionPolicy.Sequential ? [assignees[0]] : assignees;
        var bookmarks = targets.Select(assignee => Bookmark(context, assignee)).ToList();
        if (reminder is { } seconds)
        {
            // 提醒书签由引擎处理：到期对本节点实例的每个任务书签发提醒事件并按间隔再排一次，节点结束时随其他书签一起清掉
            bookmarks.Add(new BookmarkRequest
            {
                Kind = BookmarkKinds.Reminder,
                DueAt = context.Clock.GetUtcNow() + TimeSpan.FromSeconds(seconds),
                Payload = new JsonObject { [UserTaskPayloadKeys.ReminderIntervalSeconds] = seconds },
            });
        }

        return ActivityOutcome.Suspend(bookmarks);
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

        var assignees = WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Assignees]);
        var policy = WorkflowValues.ConvertOr(state[UserTaskPayloadKeys.Policy], UserTaskCompletionPolicy.Any);
        if (!string.Equals(outcome, UserTaskOutcomes.Approved, StringComparison.OrdinalIgnoreCase))
        {
            // 会签下的拒绝只记入拒绝名单，按阈值判定能否继续；其他策略与自定义结果立即以该结果完成节点
            if (policy == UserTaskCompletionPolicy.All && string.Equals(outcome, UserTaskOutcomes.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                var rejected = WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Rejected]).ToList();
                if (!string.IsNullOrWhiteSpace(actor) && !rejected.Contains(actor, StringComparer.Ordinal))
                {
                    rejected.Add(actor);
                }

                state[UserTaskPayloadKeys.Rejected] = Array(rejected);
                return ValueTask.FromResult(DecideAll(context, assignees, WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Approved]), rejected, outputs));
            }

            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, outcome.ToLowerInvariant())));
        }

        var approved = WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Approved]).ToList();
        if (!string.IsNullOrWhiteSpace(actor) && !approved.Contains(actor, StringComparer.Ordinal))
        {
            approved.Add(actor);
        }

        state[UserTaskPayloadKeys.Approved] = Array(approved);
        switch (policy)
        {
            case UserTaskCompletionPolicy.All:
                return ValueTask.FromResult(DecideAll(context, assignees, approved, WorkflowValues.ToStringList(state[UserTaskPayloadKeys.Rejected]), outputs));
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
    /// 给一个指派人的书签。
    /// </summary>
    /// <remarks>
    /// 载荷取节点状态里的标题、表单、表单键、优先级、截止时刻与抄送。
    /// </remarks>
    internal static BookmarkRequest Bookmark(ActivityContext context, string assignee)
    {
        var state = context.State;
        var payload = new JsonObject
        {
            [UserTaskPayloadKeys.Title] = state[UserTaskPayloadKeys.Title]?.DeepClone(),
            [UserTaskPayloadKeys.FormData] = state[UserTaskPayloadKeys.FormData]?.DeepClone(),
            [UserTaskPayloadKeys.CcUserIds] = state[UserTaskPayloadKeys.CcUserIds]?.DeepClone(),
        };
        CopyIfPresent(state, payload, UserTaskPayloadKeys.FormKey);
        CopyIfPresent(state, payload, UserTaskPayloadKeys.Priority);
        CopyIfPresent(state, payload, UserTaskPayloadKeys.DueAt);
        return new BookmarkRequest { Kind = BookmarkKinds.UserTask, Key = assignee, Payload = payload, CorrelationId = context.Instance.CorrelationId };
    }

    /// <summary>
    /// 解析 <see cref="ApprovalThresholdProperty"/>：正整数为人数，<c>"60%"</c> 为 1 到 100 的百分比。
    /// </summary>
    internal static bool TryParseApprovalThreshold(JsonNode? node, out bool isPercent, out int value)
    {
        isPercent = false;
        value = 0;
        if (node is not JsonValue scalar)
        {
            return false;
        }

        var text = WorkflowValues.AsString(scalar).Trim();
        if (text.EndsWith('%'))
        {
            isPercent = true;
            return int.TryParse(text[..^1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 100;
        }

        if (!WorkflowValues.TryDecimal(scalar, out var number) || number != decimal.Truncate(number) || number is < 1 or > int.MaxValue)
        {
            return false;
        }

        value = (int)number;
        return true;
    }

    /// <summary>
    /// 解析 <see cref="PriorityProperty"/>：整数（数字或数字字符串，不带小数）。
    /// </summary>
    internal static bool TryParsePriority(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue scalar || !WorkflowValues.TryDecimal(scalar, out var number) || number != decimal.Truncate(number) || number is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)number;
        return true;
    }

    /// <summary>
    /// 解析秒数属性（<see cref="DueAfterSecondsProperty"/>、<see cref="ReminderAfterSecondsProperty"/>）。
    /// </summary>
    /// <remarks>
    /// 有限正数。
    /// </remarks>
    internal static bool TryParseSeconds(JsonNode? node, out double seconds)
    {
        seconds = 0;
        return WorkflowValues.TryConvert(node, out seconds) && double.IsFinite(seconds) && seconds > 0;
    }

    /// <summary>
    /// 会签判定。
    /// </summary>
    /// <remarks>
    /// 通过数达到阈值即通过；剩下未表态的人全部通过也到不了阈值即拒绝；否则继续挂起（合并本次变量、保留其余书签）。
    /// </remarks>
    private static Result<ActivityOutcome> DecideAll(ActivityResumeContext context, IReadOnlyList<string> assignees, IReadOnlyList<string> approved, IReadOnlyList<string> rejected, JsonObject outputs)
    {
        var threshold = RequiredApprovals(context.Property(ApprovalThresholdProperty), assignees.Count);
        if (!threshold.TryGetValue(out var required))
        {
            return threshold.Error!;
        }

        if (approved.Count >= required)
        {
            return ActivityOutcome.Complete(outputs, UserTaskOutcomes.Approved);
        }

        if (assignees.Count - rejected.Count < required)
        {
            return ActivityOutcome.Complete(outputs, UserTaskOutcomes.Rejected);
        }

        return ActivityOutcome.SuspendWithOutputs(outputs);
    }

    /// <summary>
    /// 按阈值属性与当前指派人数算出需要的通过人数。
    /// </summary>
    /// <remarks>
    /// 缺省全部；百分比向上取整；人数阈值不超过指派人数、至少 1。
    /// </remarks>
    private static Result<int> RequiredApprovals(JsonNode? property, int assigneeCount)
    {
        if (property is null)
        {
            return assigneeCount;
        }

        if (!TryParseApprovalThreshold(property, out var isPercent, out var value))
        {
            return ActivityContext.PropertyInvalid(ApprovalThresholdProperty, "must be a positive integer or a percentage like '60%'");
        }

        // 阈值在每次恢复时按当前指派人数重算：加签、转办后人数变了阈值也跟着变，人数阈值超过总人数时按全部处理
        var required = isPercent ? (int)Math.Ceiling(assigneeCount * value / 100m) : value;
        return Math.Clamp(required, 1, Math.Max(1, assigneeCount));
    }

    /// <summary>
    /// 按属性算出截止时刻。
    /// </summary>
    /// <remarks>
    /// <see cref="DueAtProperty"/> 优先（ISO-8601 字面值，否则按表达式求值），其次 <see cref="DueAfterSecondsProperty"/> 相对现在；都没有为空。属性不合法返回错误。
    /// </remarks>
    private static Error? DueAt(ActivityContext context, out DateTimeOffset? dueAt)
    {
        dueAt = null;
        if (context.Property(DueAtProperty) is { } dueNode)
        {
            if (dueNode is JsonValue literal && DateTimeOffset.TryParse(WorkflowValues.AsString(literal), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fixedAt))
            {
                dueAt = fixedAt;
                return null;
            }

            var evaluated = context.EvaluateProperty(DueAtProperty);
            if (!evaluated.TryGetValue(out var value))
            {
                return evaluated.Error!;
            }

            if (value is JsonValue scalar && DateTimeOffset.TryParse(WorkflowValues.AsString(scalar), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var evaluatedAt))
            {
                dueAt = evaluatedAt;
                return null;
            }

            return ActivityContext.PropertyInvalid(DueAtProperty, "must be a date-time");
        }

        if (context.Property(DueAfterSecondsProperty) is { } afterNode)
        {
            if (!TryParseSeconds(afterNode, out var seconds))
            {
                return ActivityContext.PropertyInvalid(DueAfterSecondsProperty, "must be a positive number of seconds");
            }

            dueAt = context.Clock.GetUtcNow() + TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    /// <summary>
    /// 合并三个来源的指派人。
    /// </summary>
    /// <remarks>
    /// 列表属性、表达式属性、规则属性；裁剪、去重、保序。
    /// </remarks>
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
    /// 节点状态里的值非空时拷到载荷。
    /// </summary>
    private static void CopyIfPresent(JsonObject state, JsonObject payload, string key)
    {
        if (state.TryGetPropertyValue(key, out var value) && value is not null)
        {
            payload[key] = value.DeepClone();
        }
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
