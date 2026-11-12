// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 对 <c>Items</c>逐项启动子流程
/// </summary>
/// <remarks>
/// 表达式，求值为数组。<c>DefinitionName</c> / <c>DefinitionVersion</c> / <c>Variables</c>。每个子实例得到 <c>ItemVariable</c>（默认 <c>item</c>）与 <c>index</c>。<c>Parallel</c>（默认假）决定一起启动还是逐个；<c>FailFast</c>（默认真）时子实例未完成即故障；<c>ResultVariable</c> 接收各子实例变量的数组。空集合立即完成
/// 项数不超过引擎选项 <c>MaxForEachItems</c>，超过即属性错误、一个子实例也不启动；只有给了 <c>ResultVariable</c> 才收集子实例变量，收集的结果按 <c>MaxVariablesBytes</c> 边收边计，超过即节点故障（ADR-0182）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ForEachActivity : IResumableActivity
{
    /// <summary>
    /// 属性：集合表达式
    /// </summary>
    public const string ItemsProperty = "Items";

    /// <summary>
    /// 属性：元素变量名
    /// </summary>
    public const string ItemVariableProperty = "ItemVariable";

    /// <summary>
    /// 属性：是否并行
    /// </summary>
    public const string ParallelProperty = "Parallel";

    /// <summary>
    /// 属性：子实例失败是否立即故障
    /// </summary>
    public const string FailFastProperty = "FailFast";

    /// <summary>
    /// 状态：元素
    /// </summary>
    private const string ItemsState = "items";

    /// <summary>
    /// 状态：下一个要启动的下标
    /// </summary>
    private const string NextIndexState = "nextIndex";

    /// <summary>
    /// 状态：已完成的子实例数
    /// </summary>
    private const string CompletedState = "completed";

    /// <summary>
    /// 状态：各子实例的变量
    /// </summary>
    private const string ResultsState = "results";

    /// <summary>
    /// 状态：已收集结果的序列化字节数
    /// </summary>
    private const string ResultsBytesState = "resultsBytes";

    /// <summary>
    /// 默认元素变量名
    /// </summary>
    private const string DefaultItemVariable = "item";

    /// <summary>
    /// 子实例里的下标变量名
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

        if (itemsNode.OrNull() is not JsonArray items)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityFailures.NotArray(ItemsProperty)));
        }

        if (items.Count == 0)
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(ResultOutputs(context, []))));
        }

        // 每项一个子实例、并行时一次全部启动：项数由定义里的表达式给出，先核对再启动
        var max = context.Services.GetRequiredService<WorkflowOptions>().MaxForEachItems;
        if (items.Count > max)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityFailures.TooManyItems(ItemsProperty, items.Count, max)));
        }

        context.State[ItemsState] = items.DeepClone();
        context.State[CompletedState] = 0;
        if (CollectsResults(context))
        {
            context.State[ResultsState] = new JsonArray();
            context.State[ResultsBytesState] = 0L;
        }
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
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityFailures.TimedOut(context.Node)));
        }

        var status = context.Inputs.TryGetPropertyValue(WorkflowReservedNames.ChildStatusInput, out var statusNode) && statusNode is JsonValue statusValue ? WorkflowValues.AsString(statusValue) : string.Empty;
        if (!string.Equals(status, nameof(WorkflowInstanceStatus.Completed), StringComparison.OrdinalIgnoreCase) && context.Property(FailFastProperty, true))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityFailures.ChildFailed(context.Inputs, status)));
        }

        if (CollectsResults(context) && CollectResult(context) is { } tooLarge)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(tooLarge));
        }

        var completed = WorkflowValues.ConvertOr(context.State[CompletedState], 0) + 1;
        context.State[CompletedState] = completed;
        var items = context.State[ItemsState] as JsonArray ?? [];
        var next = WorkflowValues.ConvertOr(context.State[NextIndexState], items.Count);

        if (completed >= items.Count)
        {
            var results = context.State[ResultsState] as JsonArray;
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(ResultOutputs(context, (JsonArray?)results?.DeepClone() ?? []))));
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
    /// 是否收集子实例变量
    /// </summary>
    /// <remarks>
    /// 没给 <c>ResultVariable</c> 时结果不进变量，也就不存进节点状态
    /// </remarks>
    private static bool CollectsResults(ActivityContext context) => !string.IsNullOrWhiteSpace(context.Property<string>(SubWorkflowActivity.ResultVariableProperty));

    /// <summary>
    /// 把回调的子实例变量追加进结果
    /// </summary>
    /// <remarks>
    /// 结果最终写进变量：累计字节数超过 <c>MaxVariablesBytes</c> 即返回错误，不等全部子实例结束才发现。成功为空
    /// </remarks>
    private static Error? CollectResult(ActivityResumeContext context)
    {
        var variables = context.Inputs.TryGetPropertyValue(WorkflowReservedNames.ChildVariablesInput, out var value) ? value?.DeepClone() : null;
        var bytes = WorkflowValues.ConvertOr(context.State[ResultsBytesState], 0L) + WorkflowVariableLimit.Measure(variables) + 1;
        var max = context.Services.GetRequiredService<WorkflowOptions>().MaxVariablesBytes;
        if (bytes > max)
        {
            return Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.VariablesTooLarge, details: MessageArguments.Of(("size", bytes), ("max", max)));
        }

        // 原地追加：先从状态里取下数组（脱离父节点），追加后放回，不必每次复制整个数组
        if (context.State[ResultsState] is not JsonArray results)
        {
            results = [];
        }
        else
        {
            context.State.Remove(ResultsState);
        }

        results.Add(variables);
        context.State[ResultsState] = results;
        context.State[ResultsBytesState] = bytes;
        return null;
    }

    /// <summary>
    /// 第 <paramref name="index"/> 个元素的子流程启动请求
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
    /// 结果输出：<c>ResultVariable</c> 有值时放各子实例变量的数组
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
