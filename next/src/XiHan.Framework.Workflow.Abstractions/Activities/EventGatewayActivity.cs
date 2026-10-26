// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 事件网关：同时等待 <c>Signals</c> 列表里的任一信号与可选的 <c>TimeoutSeconds</c>，先到的一个决定出路——结果标签 = 信号名或 <c>timeout</c>，输出 <c>signal</c>、<c>payload</c>；其余书签由引擎清掉。<c>AcceptAnyCorrelation</c> 同 <c>WaitSignal</c>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class EventGatewayActivity : IResumableActivity
{
    /// <summary>属性：信号名列表（元素为模板）。</summary>
    public const string SignalsProperty = "Signals";

    /// <summary>属性：超时秒数；空为不超时。</summary>
    public const string TimeoutSecondsProperty = "TimeoutSeconds";

    /// <summary>属性：接受任意关联。</summary>
    public const string AcceptAnyCorrelationProperty = "AcceptAnyCorrelation";

    /// <summary>超时的结果标签。</summary>
    public const string TimeoutOutcome = "timeout";

    /// <summary>输出：命中的信号名。</summary>
    public const string SignalOutput = "signal";

    /// <summary>输出：信号载荷。</summary>
    public const string PayloadOutput = "payload";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var names = new List<string>();
        foreach (var raw in WorkflowValues.ToStringList(context.Property(SignalsProperty)))
        {
            var rendered = context.Evaluator.Render(raw, context.Scope());
            if (!rendered.TryGetValue(out var name))
            {
                return ValueTask.FromResult(Result.Failure<ActivityOutcome>(rendered.Error!));
            }

            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(SignalsProperty, "needs at least one signal name")));
        }

        var acceptAny = context.Property(AcceptAnyCorrelationProperty, false);
        if (!acceptAny && string.IsNullOrWhiteSpace(context.Instance.CorrelationId))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(AcceptAnyCorrelationProperty, "the instance has no correlation ID; set AcceptAnyCorrelation or start with a correlation ID")));
        }

        var bookmarks = names.Select(name => new BookmarkRequest { Kind = BookmarkKinds.Signal, Key = name, CorrelationId = acceptAny ? null : context.Instance.CorrelationId }).ToList();
        if (context.Property(TimeoutSecondsProperty) is { } timeoutNode)
        {
            if (!WorkflowValues.TryConvert<double>(timeoutNode, out var seconds) || seconds <= 0 || !double.IsFinite(seconds))
            {
                return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(TimeoutSecondsProperty, "must be a positive number of seconds")));
            }

            bookmarks.Add(new BookmarkRequest { Kind = BookmarkKinds.Timer, DueAt = context.Clock.GetUtcNow() + TimeSpan.FromSeconds(seconds) });
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(bookmarks)));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTimeout)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.EventGateway), ("reason", "The node timed out.")))));
        }

        if (string.Equals(context.Bookmark.Kind, BookmarkKinds.Timer, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(new JsonObject { [SignalOutput] = TimeoutOutcome }, TimeoutOutcome)));
        }

        var signal = context.Bookmark.Key ?? string.Empty;
        var outputs = new JsonObject { [SignalOutput] = signal, [PayloadOutput] = WorkflowJson.CloneObject(context.Inputs) };
        return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs, signal)));
    }
}
