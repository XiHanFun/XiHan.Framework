// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 对 <c>Items</c>（表达式，求值为数组）逐项启动子流程（<c>DefinitionName</c> / <c>DefinitionVersion</c> / <c>Variables</c>），每个子实例得到 <c>ItemVariable</c>（默认 <c>item</c>）与 <c>index</c>；
/// <c>Parallel</c>（默认假）决定一起启动还是逐个；<c>FailFast</c>（默认真）时子实例未完成即故障；<c>ResultVariable</c> 接收各子实例变量的数组。空集合立即完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ForEachActivity : IResumableActivity
{
    /// <summary>
    /// 属性：集合表达式。
    /// </summary>
    public const string ItemsProperty = "Items";

    /// <summary>
    /// 属性：元素变量名。
    /// </summary>
    public const string ItemVariableProperty = "ItemVariable";

    /// <summary>
    /// 属性：是否并行。
    /// </summary>
    public const string ParallelProperty = "Parallel";

    /// <summary>
    /// 属性：子实例失败是否立即故障。
    /// </summary>
    public const string FailFastProperty = "FailFast";

    /// <summary>
    /// 状态：元素。
    /// </summary>
    private const string ItemsState = "items";

    /// <summary>
    /// 状态：下一个要启动的下标。
    /// </summary>
    private const string NextIndexState = "nextIndex";

    /// <summary>
    /// 状态：已完成的子实例数。
    /// </summary>
    private const string CompletedState = "completed";

    /// <summary>
    /// 状态：各子实例的变量。
    /// </summary>
    private const string ResultsState = "results";

    /// <summary>
    /// 默认元素变量名。
    /// </summary>
    private const string DefaultItemVariable = "item";

    /// <summary>
    /// 子实例里的下标变量名。
    /// </summary>
    private const string IndexVariable = "index";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluated = context.EvaluateProperty(ItemsProperty);
        if (!evaluated.TryGetValue(out var itemsNode))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(evaluated.Error!));
        }

        if (itemsNode is not JsonArray items)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(ItemsProperty, "must evaluate to an array")));
        }

        if (items.Count == 0)
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(ResultOutputs(context, []))));
        }

        context.State[ItemsState] = items.DeepClone();
        context.State[CompletedState] = 0;
        context.State[ResultsState] = new JsonArray();
        var parallel = context.Property(ParallelProperty, false);
        var count = parallel ? items.Count : 1;
        var children = new List<WorkflowStartRequest>(count);
        for (var index = 0; index < count; index++)
        {
            var child = ChildRequest(context, items, index);
            if (!child.TryGetValue(out var request))
            {
                return ValueTask.FromResult(Result.Failure<ActivityOutcome>(child.Error!));
            }

            children.Add(request);
        }

        context.State[NextIndexState] = count;
        return ValueTask.FromResult(Result.Success(ActivityOutcome.SuspendWithChildren(children, SubWorkflowActivity.CallbackBookmark(context))));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTimeout)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.ForEach), ("reason", "The node timed out.")))));
        }

        var status = context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildStatusInput, out var statusNode) && statusNode is JsonValue statusValue ? WorkflowValues.AsString(statusValue) : string.Empty;
        if (!string.Equals(status, nameof(WorkflowInstanceStatus.Completed), StringComparison.OrdinalIgnoreCase) && context.Property(FailFastProperty, true))
        {
            var fault = context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildFaultInput, out var faultNode) && faultNode is JsonValue faultValue ? WorkflowValues.AsString(faultValue) : status;
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ChildFailed, details: MessageArguments.Of(("id", context.Inputs[WorkflowEngine.ChildInstanceIdInput]?.ToString() ?? string.Empty), ("reason", fault)))));
        }

        // 状态里的数组已有父节点，改副本再放回去（JSON 节点只能有一个父）
        var results = (context.State[ResultsState] as JsonArray)?.DeepClone() as JsonArray ?? [];
        results.Add(context.Inputs.TryGetPropertyValue(WorkflowEngine.ChildVariablesInput, out var variables) ? variables?.DeepClone() : null);
        context.State[ResultsState] = results;
        var completed = WorkflowValues.ConvertOr(context.State[CompletedState], 0) + 1;
        context.State[CompletedState] = completed;
        var items = context.State[ItemsState] as JsonArray ?? [];
        var next = WorkflowValues.ConvertOr(context.State[NextIndexState], items.Count);

        if (completed >= items.Count)
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(ResultOutputs(context, (JsonArray)results.DeepClone()))));
        }

        // 顺序模式：启动下一个；并行模式：全部已启动，只等剩下的回调
        if (next < items.Count)
        {
            var child = ChildRequest(context, items, next);
            if (!child.TryGetValue(out var request))
            {
                return ValueTask.FromResult(Result.Failure<ActivityOutcome>(child.Error!));
            }

            context.State[NextIndexState] = next + 1;
            return ValueTask.FromResult(Result.Success(ActivityOutcome.SuspendWithChildren([request], SubWorkflowActivity.CallbackBookmark(context))));
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(SubWorkflowActivity.CallbackBookmark(context))));
    }

    /// <summary>
    /// 第 <paramref name="index"/> 个元素的子流程启动请求。
    /// </summary>
    private static Result<WorkflowStartRequest> ChildRequest(ActivityContext context, JsonArray items, int index)
    {
        var itemVariable = context.Property<string>(ItemVariableProperty);
        var extra = new JsonObject
        {
            [string.IsNullOrWhiteSpace(itemVariable) ? DefaultItemVariable : itemVariable] = items[index]?.DeepClone(),
            [IndexVariable] = index,
        };
        return SubWorkflowActivity.BuildStartRequest(context, context.Property(SubWorkflowActivity.VariablesProperty) as JsonObject, extra);
    }

    /// <summary>
    /// 结果输出：<c>ResultVariable</c> 有值时放各子实例变量的数组。
    /// </summary>
    private static JsonObject ResultOutputs(ActivityContext context, JsonArray results)
    {
        var outputs = new JsonObject();
        var resultVariable = context.Property<string>(SubWorkflowActivity.ResultVariableProperty);
        if (!string.IsNullOrWhiteSpace(resultVariable))
        {
            outputs[resultVariable] = results;
        }

        return outputs;
    }
}
