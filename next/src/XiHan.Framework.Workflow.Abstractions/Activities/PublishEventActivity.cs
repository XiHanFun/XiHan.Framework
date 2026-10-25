// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 发布自定义事件：主题 <c>workflow.custom</c>，信封 <c>Subject</c> 为 <c>EventName</c>（模板），<c>Payload</c> 里的字符串按模板渲染。没有装配消息发布者时跳过。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PublishEventActivity : IActivity
{
    /// <summary>
    /// 属性：事件名。
    /// </summary>
    public const string EventNameProperty = "EventName";

    /// <summary>
    /// 属性：载荷。
    /// </summary>
    public const string PayloadProperty = "Payload";

    /// <inheritdoc />
    public async ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(EventNameProperty);
        if (!rendered.TryGetValue(out var eventName))
        {
            return rendered.Error!;
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            return ActivityContext.PropertyInvalid(EventNameProperty, "must not be empty");
        }

        var payload = new JsonObject();
        if (context.Property(PayloadProperty) is JsonObject source)
        {
            var scope = context.Scope();
            foreach (var (name, value) in source)
            {
                if (value is JsonValue scalar && scalar.TryGetValue<string>(out var template))
                {
                    var text = context.Evaluator.Render(template, scope);
                    if (!text.TryGetValue(out var renderedText))
                    {
                        return text.Error!;
                    }

                    payload[name] = renderedText;
                }
                else
                {
                    payload[name] = value?.DeepClone();
                }
            }
        }

        if (context.Services.GetService<IMessagePublisher>() is { } publisher)
        {
            var instance = context.Instance;
            var data = new WorkflowEventData
            {
                InstanceId = instance.Id,
                DefinitionId = instance.DefinitionId,
                DefinitionName = instance.DefinitionName.Value,
                DefinitionVersion = instance.DefinitionVersion,
                Status = instance.Status,
                Title = instance.Title,
                NodeId = context.Node.Id,
                NodeInstanceId = context.NodeInstance.Id,
                CorrelationId = instance.CorrelationId,
                StarterId = instance.StarterId,
                ParentInstanceId = instance.ParentInstanceId,
                TenantId = instance.TenantId,
                Payload = payload,
                At = context.Clock.GetUtcNow(),
            };
            var envelope = new MessageEnvelope
            {
                Id = WorkflowIdentifiers.NewId(),
                Source = WorkflowTopics.Source,
                Type = WorkflowTopics.Custom,
                Subject = eventName,
                Time = data.At,
                Data = WorkflowJson.SerializeEvent(data),
            };
            var published = await publisher.PublishAsync(WorkflowTopics.Custom, envelope, cancellationToken).ConfigureAwait(false);
            if (published.IsFailure)
            {
                return published.Error!;
            }
        }

        return ActivityOutcome.Complete();
    }
}
