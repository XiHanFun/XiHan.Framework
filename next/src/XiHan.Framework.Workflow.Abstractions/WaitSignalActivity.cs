// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 等待信号：以 <c>Signal</c> 书签（键 = <c>SignalName</c>，关联 = 实例关联 ID，<c>AcceptAnyCorrelation</c> 时不限）挂起；信号载荷成为节点输出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WaitSignalActivity : IActivity
{
    /// <summary>
    /// 属性：信号名。
    /// </summary>
    public const string SignalNameProperty = "SignalName";

    /// <summary>
    /// 属性：接受任意关联。
    /// </summary>
    public const string AcceptAnyCorrelationProperty = "AcceptAnyCorrelation";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(SignalNameProperty);
        if (!rendered.TryGetValue(out var signalName))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(rendered.Error!));
        }

        if (string.IsNullOrWhiteSpace(signalName))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(SignalNameProperty, "must not be empty")));
        }

        var acceptAny = context.Property(AcceptAnyCorrelationProperty, false);
        if (!acceptAny && string.IsNullOrWhiteSpace(context.Instance.CorrelationId))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(AcceptAnyCorrelationProperty, "the instance has no correlation ID; set AcceptAnyCorrelation or start with a correlation ID")));
        }

        var bookmark = new BookmarkRequest { Kind = BookmarkKinds.Signal, Key = signalName, CorrelationId = acceptAny ? null : context.Instance.CorrelationId };
        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(bookmark)));
    }
}
