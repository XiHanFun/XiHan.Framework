// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 延时：<c>Duration</c> 秒（数字或表达式，须大于 0），以 <c>Timer</c> 书签挂起，到期完成；超时书签即故障。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DelayActivity : IResumableActivity
{
    /// <summary>
    /// 属性：时长（秒）。
    /// </summary>
    public const string DurationProperty = "Duration";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluated = context.EvaluateProperty(DurationProperty);
        if (!evaluated.TryGetValue(out var value))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(evaluated.Error!));
        }

        if (!WorkflowValues.TryConvert<double>(value, out var seconds) || seconds <= 0 || !double.IsFinite(seconds))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(DurationProperty, "must be a positive number of seconds")));
        }

        var bookmark = new BookmarkRequest { Kind = BookmarkKinds.Timer, DueAt = context.Clock.GetUtcNow() + TimeSpan.FromSeconds(seconds) };
        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(bookmark)));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.IsTimeout
            ? Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.Delay), ("reason", "The node timed out."))))
            : Result.Success(ActivityOutcome.Complete()));
    }
}
