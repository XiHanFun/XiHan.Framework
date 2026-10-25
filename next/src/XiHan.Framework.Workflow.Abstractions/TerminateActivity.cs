// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 终止节点：以 <c>Reason</c> 终止实例（不补偿）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TerminateActivity : IActivity
{
    /// <summary>
    /// 属性：原因。
    /// </summary>
    public const string ReasonProperty = "Reason";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(ReasonProperty);
        return ValueTask.FromResult(rendered.TryGetValue(out var reason)
            ? Result.Success(ActivityOutcome.TerminateInstance(string.IsNullOrWhiteSpace(reason) ? null : reason))
            : Result.Failure<ActivityOutcome>(rendered.Error!));
    }
}
