// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.UserTasks;
using XiHan.Framework.Workflow.UserTasks;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 人工任务活动（审批节点：或签/会签/依次审批、超时、转办与加签由任务服务配合完成）
/// </summary>
/// <remarks>
/// 节点属性：<c>Assignees</c>（受理人标识列表）或 <c>AssigneesExpression</c>（求值为列表/逗号分隔字符串的表达式）；
/// <c>CompletionPolicy</c>（Any 或签（默认）/ All 会签 / Sequential 依次审批，未知值按定义错误故障）；
/// <c>Title</c>（任务标题，支持模板，默认节点名称）；<c>FormData</c>（表单数据字典）；<c>CcUserIds</c>（抄送人列表）；
/// <c>AllowedOutcomes</c>（允许的办理结果白名单，配置后白名单外的结果被拒绝并保留待办）。
/// 完成结果：同意走 <c>outcome == 'approved'</c> 边，拒绝走 <c>outcome == 'rejected'</c> 边，
/// 节点超时走 <c>outcome == 'timeout'</c> 边，也允许业务自定义结果值（任一非同意结果立即结束节点）。
/// 恢复输入未携带办理结果时拒绝恢复并重建待办（防止裸书签恢复被当作自动同意）。
/// 中间审批人附带的业务变量在挂起时即合并进实例变量；审批轨迹保存在节点实例私有状态 <c>history</c> 中。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.UserTask, DisplayName = "人工任务", Category = "人工")]
public class UserTaskActivity : WorkflowActivityBase, IResumableWorkflowActivity
{
    private const string AssigneesStateKey = "assignees";
    private const string ApprovedStateKey = "approved";
    private const string PolicyStateKey = "policy";
    private const string HistoryStateKey = "history";
    private const string TitleStateKey = "title";
    private const string FormDataStateKey = "formData";
    private const string CcUserIdsStateKey = "ccUserIds";

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var assignees = await ResolveAssigneesAsync(context);
        if (assignees.Count == 0)
        {
            return ActivityExecutionResult.Fault($"人工任务节点 {context.Node.Id} 未配置受理人");
        }

        // fail-closed：未知完成策略是定义错误，不允许静默降级为或签削弱审批强度
        var policyText = GetProperty<string>(context, "CompletionPolicy");
        if (!TryResolvePolicy(policyText, out var policy))
        {
            return ActivityExecutionResult.Fault(
                $"人工任务节点 {context.Node.Id} 配置了未知完成策略 {policyText}（仅支持 Any/All/Sequential）");
        }

        var title = await GetTemplatedStringAsync(context, "Title") ?? context.Node.Name;

        // 复制表单数据，避免与流程定义共享同一字典实例
        var formData = new Dictionary<string, object?>(GetProperty<Dictionary<string, object?>>(context, "FormData") ?? []);
        var ccUserIds = GetProperty<List<string>>(context, "CcUserIds") ?? [];

        var state = context.NodeInstance.State;
        state[AssigneesStateKey] = assignees;
        state[ApprovedStateKey] = new List<string>();
        state[PolicyStateKey] = policy.ToString();
        state[HistoryStateKey] = new List<Dictionary<string, object?>>();
        state[TitleStateKey] = title;
        state[FormDataStateKey] = formData;
        state[CcUserIdsStateKey] = ccUserIds;

        var targets = policy == UserTaskCompletionPolicy.Sequential ? [assignees[0]] : assignees;
        var bookmarks = targets
            .Select(assignee => CreateBookmarkRequest(assignee, title, formData, ccUserIds))
            .ToArray();

        return ActivityExecutionResult.Suspend(bookmarks);
    }

    /// <inheritdoc />
    public Task<ActivityExecutionResult> ResumeAsync(ActivityResumeContext context)
    {
        var state = context.NodeInstance.State;
        var history = GetStateList<Dictionary<string, object?>>(state, HistoryStateKey);

        // 节点超时：按超时结果结束节点
        if (context.Bookmark.Kind == WorkflowBookmarkKinds.NodeTimeout)
        {
            AppendHistory(state, history, actorId: null, WorkflowUserTaskOutcomes.Timeout, comment: "节点等待超时", context);
            return Task.FromResult(ActivityExecutionResult.Complete(outcome: WorkflowUserTaskOutcomes.Timeout));
        }

        var actorId = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowUserTaskInputKeys.ActorId)) ?? context.Bookmark.Key ?? string.Empty;
        var outcome = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowUserTaskInputKeys.Outcome));
        var comment = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowUserTaskInputKeys.Comment));

        // fail-closed：未携带办理结果的恢复（如运维误踢书签）不得视为自动同意，拒绝并重建待办
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return Task.FromResult(RebuildBookmark(context, "恢复输入未携带办理结果"));
        }

        // 结果白名单校验：白名单外的结果拒绝办理并重建待办，避免拼写错误直接故障实例、丢失会签进度
        var allowedOutcomes = GetProperty<List<string>>(context, "AllowedOutcomes");
        if (allowedOutcomes is { Count: > 0 } && !allowedOutcomes.Contains(outcome, StringComparer.Ordinal))
        {
            return Task.FromResult(RebuildBookmark(context, $"办理结果 {outcome} 不在允许列表内"));
        }

        AppendHistory(state, history, actorId, outcome, comment, context);

        // 办理时附带的业务变量作为输出合并进实例变量
        var outputs = context.Inputs
            .Where(pair => pair.Key is not (WorkflowUserTaskInputKeys.ActorId or WorkflowUserTaskInputKeys.Outcome or WorkflowUserTaskInputKeys.Comment))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        // 任一非同意结果（拒绝/自定义）立即结束节点
        if (!string.Equals(outcome, WorkflowUserTaskOutcomes.Approved, StringComparison.Ordinal))
        {
            return Task.FromResult(ActivityExecutionResult.Complete(outputs, outcome));
        }

        var policy = Enum.TryParse<UserTaskCompletionPolicy>(
            WorkflowValueConverter.ConvertTo<string>(state.GetValueOrDefault(PolicyStateKey)), out var parsedPolicy)
            ? parsedPolicy
            : UserTaskCompletionPolicy.Any;

        if (policy == UserTaskCompletionPolicy.Any)
        {
            return Task.FromResult(ActivityExecutionResult.Complete(outputs, WorkflowUserTaskOutcomes.Approved));
        }

        var assignees = GetStateList<string>(state, AssigneesStateKey);
        var approved = GetStateList<string>(state, ApprovedStateKey);
        if (!approved.Contains(actorId))
        {
            approved.Add(actorId);
        }

        state[ApprovedStateKey] = approved;

        if (policy == UserTaskCompletionPolicy.All)
        {
            var pending = assignees.Except(approved, StringComparer.Ordinal).ToList();
            if (pending.Count == 0)
            {
                return Task.FromResult(ActivityExecutionResult.Complete(outputs, WorkflowUserTaskOutcomes.Approved));
            }

            // 中间审批人的业务变量即时合并，供后续审批与并行分支引用
            context.Variables.Merge(outputs);
            return Task.FromResult(ActivityExecutionResult.Suspend());
        }

        // 依次审批：为下一位受理人生成待办
        if (approved.Count >= assignees.Count)
        {
            return Task.FromResult(ActivityExecutionResult.Complete(outputs, WorkflowUserTaskOutcomes.Approved));
        }

        var next = assignees[approved.Count];
        var title = WorkflowValueConverter.ConvertTo<string>(state.GetValueOrDefault(TitleStateKey)) ?? context.Node.Name;
        var formData = WorkflowValueConverter.ConvertTo<Dictionary<string, object?>>(state.GetValueOrDefault(FormDataStateKey)) ?? [];
        var ccUserIds = WorkflowValueConverter.ConvertTo<List<string>>(state.GetValueOrDefault(CcUserIdsStateKey)) ?? [];

        context.Variables.Merge(outputs);
        return Task.FromResult(ActivityExecutionResult.Suspend(CreateBookmarkRequest(next, title, formData, ccUserIds)));
    }

    /// <summary>
    /// 拒绝本次恢复并原样重建待办书签（引擎已消费原书签）
    /// </summary>
    private static ActivityExecutionResult RebuildBookmark(ActivityResumeContext context, string reason)
    {
        var logger = context.ServiceProvider.GetService<ILogger<UserTaskActivity>>();
        logger?.LogWarning("人工任务节点 {NodeId} 拒绝恢复：{Reason}，已重建待办", context.Node.Id, reason);

        return ActivityExecutionResult.Suspend(new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = context.Bookmark.Key,
            Payload = new Dictionary<string, object?>(context.Bookmark.Payload),
            CorrelationId = context.Bookmark.CorrelationId
        });
    }

    private static WorkflowBookmarkRequest CreateBookmarkRequest(
        string assignee,
        string title,
        Dictionary<string, object?> formData,
        List<string> ccUserIds)
    {
        return new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.UserTask,
            Key = assignee,
            Payload = new Dictionary<string, object?>
            {
                [WorkflowUserTaskPayloadKeys.Title] = title,
                [WorkflowUserTaskPayloadKeys.FormData] = formData,
                [WorkflowUserTaskPayloadKeys.CcUserIds] = ccUserIds
            }
        };
    }

    private static async Task<List<string>> ResolveAssigneesAsync(ActivityExecutionContext context)
    {
        var assignees = GetProperty<List<string>>(context, "Assignees");
        if (assignees is null)
        {
            var evaluated = await EvaluatePropertyAsync(context, "AssigneesExpression");
            assignees = evaluated switch
            {
                null => [],
                string text => [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                _ => WorkflowValueConverter.ConvertTo<List<string>>(evaluated) ?? []
            };
        }

        return assignees
            .Where(assignee => !string.IsNullOrWhiteSpace(assignee))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool TryResolvePolicy(string? text, out UserTaskCompletionPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            policy = UserTaskCompletionPolicy.Any;
            return true;
        }

        return Enum.TryParse(text, ignoreCase: true, out policy) && Enum.IsDefined(policy);
    }

    private static List<T> GetStateList<T>(Dictionary<string, object?> state, string key)
    {
        return WorkflowValueConverter.ConvertTo<List<T>>(state.GetValueOrDefault(key)) ?? [];
    }

    private static void AppendHistory(
        Dictionary<string, object?> state,
        List<Dictionary<string, object?>> history,
        string? actorId,
        string outcome,
        string? comment,
        ActivityResumeContext context)
    {
        history.Add(new Dictionary<string, object?>
        {
            ["actorId"] = actorId,
            ["outcome"] = outcome,
            ["comment"] = comment,
            ["nodeId"] = context.Node.Id
        });
        state[HistoryStateKey] = history;
    }
}
