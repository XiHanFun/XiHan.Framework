// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 故障节点：以 <c>Message</c>（模板）让节点故障（走重试、继续或实例故障）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FaultActivity : IActivity
{
    /// <summary>
    /// 属性：消息。
    /// </summary>
    public const string MessageProperty = "Message";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(MessageProperty);
        var message = rendered.TryGetValue(out var text) ? text : rendered.Error!.Message;
        return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.Fault), ("reason", message)))));
    }
}
