// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 子流程。
/// </summary>
/// <remarks>
/// 按 <c>DefinitionName</c> / <c>DefinitionVersion</c> 启动子实例（变量来自 <c>Variables</c> 字面量与 <c>VariableExpressions</c>），<c>WaitForCompletion</c>（默认真）时以 <c>SubWorkflow</c> 书签等待回调：子实例未完成且 <c>FailOnChildFault</c>（默认真）即故障，否则以子实例状态小写为结果完成，<c>ResultVariable</c> 接收子实例变量。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SubWorkflowActivity : IResumableActivity
{
    /// <summary>
    /// 属性：子流程定义名。
    /// </summary>
    public const string DefinitionNameProperty = "DefinitionName";

    /// <summary>
    /// 属性：子流程定义版本。
    /// </summary>
    public const string DefinitionVersionProperty = "DefinitionVersion";

    /// <summary>
    /// 属性：传给子流程的变量（字面量）。
    /// </summary>
    public const string VariablesProperty = "Variables";

    /// <summary>
    /// 属性：传给子流程的变量（表达式）。
    /// </summary>
    public const string VariableExpressionsProperty = "VariableExpressions";

    /// <summary>
    /// 属性：是否等待子流程完成。
    /// </summary>
    public const string WaitForCompletionProperty = "WaitForCompletion";

    /// <summary>
    /// 属性：接收子实例变量的变量名。
    /// </summary>
    public const string ResultVariableProperty = "ResultVariable";

    /// <summary>
    /// 属性：子实例未完成是否故障。
    /// </summary>
    public const string FailOnChildFaultProperty = "FailOnChildFault";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = BuildStartRequest(context, context.Property(VariablesProperty) as JsonObject, null);
        if (!request.TryGetValue(out var start))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(request.Error!));
        }

        if (!context.Property(WaitForCompletionProperty, true))
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.CompleteWithChildren([start])));
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.SuspendWithChildren([start], CallbackBookmark(context))));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTimeout)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.SubWorkflow), ("reason", "The node timed out.")))));
        }

        var status = context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildStatusInput, out var statusNode) && statusNode is JsonValue statusValue ? WorkflowValues.AsString(statusValue) : string.Empty;
        var completed = string.Equals(status, nameof(WorkflowInstanceStatus.Completed), StringComparison.OrdinalIgnoreCase);
        if (!completed && context.Property(FailOnChildFaultProperty, true))
        {
            var fault = context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildFaultInput, out var faultNode) && faultNode is JsonValue faultValue ? WorkflowValues.AsString(faultValue) : status;
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, details: MessageArguments.Of(("id", context.Inputs[WorkflowEngine.ChildInstanceIdInput]?.ToString() ?? string.Empty), ("reason", fault)))));
        }

        var outputs = new JsonObject();
        var resultVariable = context.Property<string>(ResultVariableProperty);
        if (!string.IsNullOrWhiteSpace(resultVariable) && context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildVariablesInput, out var variables))
        {
            outputs[resultVariable] = variables?.DeepClone();
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, completed ? "completed" : status.ToLowerInvariant())));
    }

    /// <summary>
    /// 组装子流程启动请求：名字（模板）、版本、字面量变量加表达式变量（再加 <paramref name="extra"/>）。
    /// </summary>
    internal static Result<WorkflowStartRequest> BuildStartRequest(ActivityContext context, JsonObject? literals, JsonObject? extra)
    {
        var rendered = context.RenderProperty(DefinitionNameProperty);
        if (!rendered.TryGetValue(out var name))
        {
            return rendered.Error!;
        }

        if (!WorkflowName.IsValid(name))
        {
            return ActivityContext.PropertyInvalid(DefinitionNameProperty, $"'{name}' is not a workflow name");
        }

        var variables = WorkflowJson.CloneObject(literals);
        if (context.Property(VariableExpressionsProperty) is JsonObject expressions)
        {
            var scope = context.Scope();
            foreach (var (variable, expression) in expressions)
            {
                if (expression is not JsonValue value)
                {
                    return ActivityContext.PropertyInvalid(VariableExpressionsProperty, $"'{variable}' is not an expression");
                }

                var evaluated = context.Evaluator.Evaluate(WorkflowValues.AsString(value), scope);
                if (!evaluated.TryGetValue(out var result))
                {
                    return evaluated.Error!;
                }

                variables[variable] = result.OrNull()?.DeepClone();
            }
        }

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                variables[key] = value?.DeepClone();
            }
        }

        return new WorkflowStartRequest
        {
            Name = new WorkflowName(name),
            Version = context.Property<int?>(DefinitionVersionProperty),
            Variables = variables,
            CorrelationId = context.Instance.CorrelationId,
            StarterId = context.Instance.StarterId,
        };
    }

    /// <summary>
    /// 等待回调的书签：键是本节点实例 ID。
    /// </summary>
    internal static BookmarkRequest CallbackBookmark(ActivityContext context) => new() { Kind = BookmarkKinds.SubWorkflow, Key = context.NodeInstance.Id };
}
