// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 把攒下的事件以 CloudEvents 信封按主题批量发布；没有装配消息发布者时跳过，发布失败只记日志。
/// </summary>
internal sealed partial class WorkflowEventPublisher
{
    /// <summary>
    /// 发布者；没有为 <see langword="null"/>。
    /// </summary>
    private readonly IMessagePublisher? _publisher;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建。
    /// </summary>
    public WorkflowEventPublisher(IMessagePublisher? publisher, ILogger logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// 发布一组事件：按主题分组，每组一次批量发布。
    /// </summary>
    public async ValueTask FlushAsync(IReadOnlyList<PendingEvent> events, CancellationToken cancellationToken)
    {
        if (_publisher is null || events.Count == 0)
        {
            return;
        }

        foreach (var group in events.GroupBy(static pending => pending.Topic, StringComparer.Ordinal))
        {
            var envelopes = group.Select(static pending => Envelope(pending)).ToList();
            try
            {
                var results = await _publisher.PublishManyAsync(group.Key, envelopes, cancellationToken).ConfigureAwait(false);
                for (var index = 0; index < results.Count; index++)
                {
                    if (results[index].IsFailure)
                    {
                        LogPublishFailed(_logger, group.Key, envelopes[index].Id, results[index].Error?.Code ?? string.Empty);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // 事件是旁路：发布失败不能影响工作流状态（状态已写回存储）
                LogPublishThrew(_logger, exception, group.Key);
            }
        }
    }

    /// <summary>
    /// 事件 → 信封：来源 <c>/workflow</c>，类型为主题，租户与关联进扩展属性。
    /// </summary>
    private static MessageEnvelope Envelope(PendingEvent pending)
    {
        var envelope = new MessageEnvelope
        {
            Id = WorkflowIdentifiers.NewId(),
            Source = WorkflowTopics.Source,
            Type = pending.Topic,
            Time = pending.Data.At,
            Subject = pending.Subject ?? pending.Data.InstanceId,
            Data = WorkflowJson.SerializeEvent(pending.Data),
        };
        if (pending.Data.TenantId is { Length: > 0 } tenant)
        {
            envelope = envelope.WithExtension("tenant", tenant);
        }

        if (pending.Data.CorrelationId is { Length: > 0 } correlation)
        {
            envelope = envelope.WithExtension("correlation", correlation);
        }

        return envelope;
    }

    /// <summary>
    /// 发布返回失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing workflow event '{Topic}' (envelope '{EnvelopeId}') failed with '{ErrorCode}'.")]
    private static partial void LogPublishFailed(ILogger logger, string topic, string envelopeId, string errorCode);

    /// <summary>
    /// 发布抛出异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing workflow events on topic '{Topic}' threw.")]
    private static partial void LogPublishThrew(ILogger logger, Exception exception, string topic);
}
