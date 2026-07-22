// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 子流程活动（启动子实例，可选等待其终态回调）
/// </summary>
/// <remarks>
/// 节点属性：<c>DefinitionCode</c>（子流程编码，支持模板）；<c>DefinitionVersion</c>（为空取最新已发布版本）；
/// <c>Variables</c>（传给子流程的字面量变量字典）；<c>VariableExpressions</c>（变量名 → 表达式，求值后传入）；
/// <c>WaitForCompletion</c>（是否等待子流程结束，默认 true；false 为发后不理）；
/// <c>ResultVariable</c>（子流程变量快照写入的变量名）；
/// <c>FailOnChildFault</c>（子流程非正常结束是否故障本实例，默认 true；false 时以子流程终态小写值作为 outcome 继续流转）。
/// 子实例由引擎在本执行批次结束、释放实例锁后启动，避免父子实例锁重入。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.SubWorkflow, DisplayName = "子流程", Category = "流程控制")]
public class SubWorkflowActivity : WorkflowActivityBase, IResumableWorkflowActivity
{
    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var definitionCode = await GetTemplatedStringAsync(context, "DefinitionCode");
        if (string.IsNullOrWhiteSpace(definitionCode))
        {
            return ActivityExecutionResult.Fault($"子流程节点 {context.Node.Id} 未配置 DefinitionCode");
        }

        var childRequest = new WorkflowStartRequest
        {
            DefinitionCode = definitionCode,
            DefinitionVersion = GetProperty<int?>(context, "DefinitionVersion"),
            CorrelationId = context.Instance.CorrelationId,
            StarterId = context.Instance.StarterId,
            Variables = await BuildChildVariablesAsync(context)
        };

        var waitForCompletion = GetProperty<bool?>(context, "WaitForCompletion") ?? true;
        if (!waitForCompletion)
        {
            // 发后不理：节点直接完成，子实例在批次收尾后启动
            return ActivityExecutionResult.CompleteWithChildren([childRequest]);
        }

        return ActivityExecutionResult.SuspendWithChildren(
            [childRequest],
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
            return Task.FromResult(ActivityExecutionResult.Fault($"子流程节点 {context.Node.Id} 等待超时"));
        }

        var childInstanceId = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildInstanceIdInputKey));
        var childStatusText = WorkflowValueConverter.ConvertTo<string>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildStatusInputKey)) ?? string.Empty;
        var childVariables = WorkflowValueConverter.ConvertTo<Dictionary<string, object?>>(
            context.Inputs.GetValueOrDefault(WorkflowConsts.ChildVariablesInputKey)) ?? [];

        var succeeded = string.Equals(childStatusText, nameof(WorkflowInstanceStatus.Completed), StringComparison.OrdinalIgnoreCase);
        var failOnChildFault = GetProperty<bool?>(context, "FailOnChildFault") ?? true;

        if (!succeeded && failOnChildFault)
        {
            var childFaultMessage = WorkflowValueConverter.ConvertTo<string>(
                context.Inputs.GetValueOrDefault(WorkflowConsts.ChildFaultMessageInputKey));
            return Task.FromResult(ActivityExecutionResult.Fault(
                $"子流程节点 {context.Node.Id} 的子实例 {childInstanceId} 以 {childStatusText} 结束"
                + (string.IsNullOrEmpty(childFaultMessage) ? string.Empty : $"：{childFaultMessage}")));
        }

        var outputs = new Dictionary<string, object?>();
        var resultVariable = GetProperty<string>(context, "ResultVariable");
        if (!string.IsNullOrWhiteSpace(resultVariable))
        {
            outputs[resultVariable] = childVariables;
        }

        var outcome = succeeded ? "completed" : childStatusText.ToLowerInvariant();
        return Task.FromResult(ActivityExecutionResult.Complete(outputs, outcome));
    }

    private static async Task<Dictionary<string, object?>> BuildChildVariablesAsync(ActivityExecutionContext context)
    {
        var variables = new Dictionary<string, object?>();

        var literals = GetProperty<Dictionary<string, object?>>(context, "Variables");
        if (literals is not null)
        {
            foreach (var pair in literals)
            {
                variables[pair.Key] = WorkflowValueConverter.Normalize(pair.Value);
            }
        }

        var expressions = GetProperty<Dictionary<string, string>>(context, "VariableExpressions");
        if (expressions is not null)
        {
            var evaluator = GetEvaluator(context);
            foreach (var pair in expressions)
            {
                variables[pair.Key] = await evaluator.EvaluateAsync(pair.Value, context.Variables.AsReadOnly, context.CancellationToken);
            }
        }

        return variables;
    }
}
