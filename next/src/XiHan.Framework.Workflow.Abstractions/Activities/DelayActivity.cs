// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 延时。
/// </summary>
/// <remarks>
/// <c>Duration</c>（秒，或求值为秒数的表达式）、<c>Until</c>（时刻，或求值为时刻的表达式）、<c>Cron</c>（+ <c>TimeZone</c>，下一次匹配时刻）三选一，以 Timer 书签挂起到该时刻。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DelayActivity : IResumableActivity
{
    /// <summary>
    /// 属性：秒数或表达式。
    /// </summary>
    public const string DurationProperty = "Duration";

    /// <summary>
    /// 属性：时刻或表达式。
    /// </summary>
    public const string UntilProperty = "Until";

    /// <summary>
    /// 属性：cron 表达式。
    /// </summary>
    public const string CronProperty = "Cron";

    /// <summary>
    /// 属性：cron 的时区 ID。
    /// </summary>
    public const string TimeZoneProperty = "TimeZone";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var due = DueAt(context);
        return ValueTask.FromResult(due.TryGetValue(out var at)
            ? Result.Success(ActivityOutcome.Suspend(new BookmarkRequest { Kind = BookmarkKinds.Timer, DueAt = at }))
            : Result.Failure<ActivityOutcome>(due.Error!));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(context.IsTimeout
            ? Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.Delay), ("reason", "The node timed out."))))
            : Result.Success(ActivityOutcome.Complete()));
    }

    /// <summary>
    /// 按属性算出到期时刻。
    /// </summary>
    private static Result<DateTimeOffset> DueAt(ActivityContext context)
    {
        var now = context.Clock.GetUtcNow();
        if (context.Property(CronProperty) is not null)
        {
            var rendered = context.RenderProperty(CronProperty);
            if (!rendered.TryGetValue(out var cronText))
            {
                return rendered.Error!;
            }

            if (!CronExpression.TryParse(cronText, out var cron))
            {
                return ActivityContext.PropertyInvalid(CronProperty, $"'{cronText}' is not a cron expression");
            }

            var zoneId = context.Property<string>(TimeZoneProperty);
            Maybe<DateTimeOffset> next;
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                next = cron.Next(now);
            }
            else if (TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone))
            {
                next = cron.Next(now, zone);
            }
            else
            {
                return ActivityContext.PropertyInvalid(TimeZoneProperty, $"'{zoneId}' is not a time zone");
            }

            return next.TryGetValue(out var at) ? at : ActivityContext.PropertyInvalid(CronProperty, "has no next occurrence within five years");
        }

        if (context.Property(UntilProperty) is not null)
        {
            var evaluated = context.EvaluateProperty(UntilProperty);
            if (!evaluated.TryGetValue(out var value))
            {
                return evaluated.Error!;
            }

            if (value is JsonValue scalar && DateTimeOffset.TryParse(WorkflowValues.AsString(scalar), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var until))
            {
                return until;
            }

            return ActivityContext.PropertyInvalid(UntilProperty, "must be a date-time");
        }

        var duration = context.EvaluateProperty(DurationProperty);
        if (!duration.TryGetValue(out var seconds))
        {
            return duration.Error!;
        }

        if (!WorkflowValues.TryConvert<double>(seconds, out var total) || total <= 0 || !double.IsFinite(total))
        {
            return ActivityContext.PropertyInvalid(DurationProperty, "must be a positive number of seconds");
        }

        return now + TimeSpan.FromSeconds(total);
    }
}
