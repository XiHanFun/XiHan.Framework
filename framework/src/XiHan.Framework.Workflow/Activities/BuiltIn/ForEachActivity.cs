#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ForEachActivity
// Guid:53da806e-c491-4b27-9f65-e08d31c74af2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:17:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 遍历活动（对集合逐项执行子流程，支持顺序与并行两种模式）
/// </summary>
/// <remarks>
/// 节点属性：<c>ItemsExpression</c>（求值为集合的表达式）；<c>ItemVariableName</c>（子流程中当前项变量名，默认 item）；
/// <c>DefinitionCode</c>（子流程编码，支持模板）；<c>DefinitionVersion</c>（为空取最新已发布版本）；
/// <c>Variables</c>（附加传给每个子流程的字面量变量字典）；
/// <c>Parallel</c>（是否并行启动全部子流程，默认 false 即顺序执行）；
/// <c>FailFast</c>（任一子流程非正常结束是否立即故障，默认 true）；
/// <c>ResultVariable</c>（各子流程变量快照列表写入的变量名）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.ForEach, DisplayName = "遍历", Category = "流程控制")]
public class ForEachActivity : WorkflowActivityBase, IResumableWorkflowActivity
{
    private const string ItemsStateKey = "items";
    private const string CompletedCountStateKey = "completedCount";
    private const string NextIndexStateKey = "nextIndex";
    private const string ResultsStateKey = "results";
    private const string DefinitionCodeStateKey = "definitionCode";

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var definitionCode = await GetTemplatedStringAsync(context, "DefinitionCode");
        if (string.IsNullOrWhiteSpace(definitionCode))
        {
            return ActivityExecutionResult.Fault($"遍历节点 {context.Node.Id} 未配置 DefinitionCode");
        }

        var evaluated = await EvaluatePropertyAsync(context, "ItemsExpression");
        var items = WorkflowValueConverter.ConvertTo<List<object?>>(evaluated) ?? [];

        var resultVariable = GetProperty<string>(context, "ResultVariable");
        if (items.Count == 0)
        {
            var emptyOutputs = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(resultVariable))
            {
                emptyOutputs[resultVariable] = new List<object?>();
            }

            return ActivityExecutionResult.Complete(emptyOutputs);
        }

        var parallel = GetProperty<bool>(context, "Parallel");

        var state = context.NodeInstance.State;
        state[ItemsStateKey] = items;
        state[CompletedCountStateKey] = 0;
        state[ResultsStateKey] = new List<object?>();
        state[DefinitionCodeStateKey] = definitionCode;
        state[NextIndexStateKey] = parallel ? items.Count : 1;

        var childRequests = parallel
            ? items.Select((item, index) => BuildChildRequest(context, definitionCode, item, index)).ToList()
            : [BuildChildRequest(context, definitionCode, items[0], 0)];

        return ActivityExecutionResult.SuspendWithChildren(
            childRequests,
            new WorkflowBookmarkRequest
            {
                Kind = WorkflowBookmarkKinds.SubWorkflow,
                Key = context.NodeInstance.Id
            });
    }

    /// <inheritdoc />
    public Task<ActivityExecutionResult> ResumeAsync(ActivityResumeContext context)
    {
        if (context.Bookmark.Kind == WorkflowBookmarkKinds.NodeTimeout)
        {
            return Task.FromResult(ActivityExecutionResult.Fault($"遍历节点 {context.Node.Id} 等待超时"));
        }

        var state = context.NodeInstance.State;
        var items = WorkflowValueConverter.ConvertTo<List<object?>>(state.GetValueOrDefault(ItemsStateKey)) ?? [];
        var completedCount = WorkflowValueConverter.ConvertTo<int>(state.GetValueOrDefault(CompletedCountStateKey));
        var nextIndex = WorkflowValueConverter.ConvertTo<int>(state.GetValueOrDefault(NextIndexStateKey));
        var results = WorkflowValueConverter.ConvertTo<List<object?>>(state.GetValueOrDefault(ResultsStateKey)) ?? [];
        var definitionCode = WorkflowValueConverter.ConvertTo<string>(state.GetValueOrDefault(DefinitionCodeStateKey)) ?? string.Empty;

        var childInstanceId = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildInstanceIdInputKey));
        var childStatusText = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildStatusInputKey)) ?? string.Empty;
        var childVariables = WorkflowValueConverter.ConvertTo<Dictionary<string, object?>>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildVariablesInputKey));

        completedCount++;
        results.Add(childVariables);
        state[CompletedCountStateKey] = completedCount;
        state[ResultsStateKey] = results;

        var succeeded = string.Equals(childStatusText, nameof(WorkflowInstanceStatus.Completed), StringComparison.OrdinalIgnoreCase);
        var failFast = GetProperty<bool?>(context, "FailFast") ?? true;
        if (!succeeded && failFast)
        {
            var childFaultMessage = WorkflowValueConverter.ConvertTo<string>(
                context.Inputs.GetValueOrDefault(WorkflowConsts.ChildFaultMessageInputKey));
            return Task.FromResult(ActivityExecutionResult.Fault(
                $"遍历节点 {context.Node.Id} 的子实例 {childInstanceId} 以 {childStatusText} 结束"
                + (string.IsNullOrEmpty(childFaultMessage) ? string.Empty : $"：{childFaultMessage}")));
        }

        // 全部完成
        if (completedCount >= items.Count)
        {
            var outputs = new Dictionary<string, object?>();
            var resultVariable = GetProperty<string>(context, "ResultVariable");
            if (!string.IsNullOrWhiteSpace(resultVariable))
            {
                outputs[resultVariable] = results;
            }

            return Task.FromResult(ActivityExecutionResult.Complete(outputs));
        }

        // 书签已被消费，重新声明等待点；顺序模式追加启动下一个子实例
        var bookmark = new WorkflowBookmarkRequest
        {
            Kind = WorkflowBookmarkKinds.SubWorkflow,
            Key = context.NodeInstance.Id
        };

        if (nextIndex < items.Count)
        {
            var childRequest = BuildChildRequest(context, definitionCode, items[nextIndex], nextIndex);
            state[NextIndexStateKey] = nextIndex + 1;
            return Task.FromResult(ActivityExecutionResult.SuspendWithChildren([childRequest], bookmark));
        }

        return Task.FromResult(ActivityExecutionResult.Suspend(bookmark));
    }

    private static WorkflowStartRequest BuildChildRequest(
        ActivityExecutionContext context,
        string definitionCode,
        object? item,
        int index)
    {
        var itemVariableName = GetProperty<string>(context, "ItemVariableName") ?? "item";
        var variables = new Dictionary<string, object?>
        {
            [itemVariableName] = WorkflowValueConverter.Normalize(item),
            ["index"] = index
        };

        var extra = GetProperty<Dictionary<string, object?>>(context, "Variables");
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                variables[pair.Key] = WorkflowValueConverter.Normalize(pair.Value);
            }
        }

        return new WorkflowStartRequest
        {
            DefinitionCode = definitionCode,
            DefinitionVersion = GetProperty<int?>(context, "DefinitionVersion"),
            CorrelationId = context.Instance.CorrelationId,
            StarterId = context.Instance.StarterId,
            Variables = variables
        };
    }
}
