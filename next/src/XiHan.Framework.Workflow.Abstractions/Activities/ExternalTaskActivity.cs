// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.ExternalTasks;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Values;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 外部任务：以 <c>External</c> 书签（<c>Key</c> = 主题）挂起，由外部工作者经 <c>IWorkflowExternalTaskService</c> 领取并完成；输出合并进实例变量。锁到期的书签由轮询恢复，活动重新挂起（解锁）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class ExternalTaskActivity : IResumableActivity
{
    /// <summary>
    /// 属性：主题（模板）。
    /// </summary>
    public const string TopicProperty = "Topic";

    /// <summary>
    /// 属性：交给工作者的载荷（对象；字符串值按模板渲染）。
    /// </summary>
    public const string PayloadProperty = "Payload";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(TopicProperty);
        if (!rendered.TryGetValue(out var topic))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(rendered.Error!));
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(ActivityContext.PropertyInvalid(TopicProperty, "must not be empty")));
        }

        var payload = new JsonObject();
        if (context.Property(PayloadProperty) is JsonObject declared)
        {
            // 字符串值按模板渲染，其他值原样
            foreach (var (key, value) in declared)
            {
                if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text))
                {
                    var renderedValue = context.Evaluator.Render(text, context.Scope());
                    if (!renderedValue.TryGetValue(out var renderedText))
                    {
                        return ValueTask.FromResult(Result.Failure<ActivityOutcome>(renderedValue.Error!));
                    }

                    payload[key] = renderedText;
                }
                else
                {
                    payload[key] = value?.DeepClone();
                }
            }
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(new BookmarkRequest
        {
            Kind = BookmarkKinds.External,
            Key = topic,
            Payload = new JsonObject { [ExternalTaskPayloadKeys.Topic] = topic, [ExternalTaskPayloadKeys.Payload] = payload, [ExternalTaskPayloadKeys.Attempt] = 1 },
            CorrelationId = context.Instance.CorrelationId,
        })));
    }

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ResumeAsync(ActivityResumeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsTimeout)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ActivityFailed, details: MessageArguments.Of(("type", ActivityTypes.ExternalTask), ("reason", "The node timed out.")))));
        }

        if (context.Inputs.TryGetPropertyValue(ExternalTaskPayloadKeys.Outputs, out var outputs))
        {
            return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete(outputs as JsonObject)));
        }

        if (context.Inputs.TryGetPropertyValue(ExternalTaskPayloadKeys.Error, out var error) && error is JsonValue errorValue)
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(Error.Unexpected(WorkflowErrors.ActivityFailed, WorkflowMessages.ExternalTaskFailed, details: MessageArguments.Of(("reason", WorkflowValues.AsString(errorValue))))));
        }

        // 没有输入：锁到期或重试时刻到了，原样重新挂起（去掉锁），工作者可再次领取
        var payload = WorkflowJson.CloneObject(context.Bookmark.Payload);
        payload.Remove(ExternalTaskPayloadKeys.LockedBy);
        payload.Remove(ExternalTaskPayloadKeys.LockedUntil);
        payload.Remove(ExternalTaskPayloadKeys.NotBefore);
        return ValueTask.FromResult(Result.Success(ActivityOutcome.Suspend(new BookmarkRequest
        {
            Kind = BookmarkKinds.External,
            Key = context.Bookmark.Key,
            Payload = payload,
            CorrelationId = context.Bookmark.CorrelationId,
        })));
    }
}
