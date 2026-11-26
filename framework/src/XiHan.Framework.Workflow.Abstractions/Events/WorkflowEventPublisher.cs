// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Serialization;

namespace XiHan.Framework.Workflow.Abstractions.Events;

/// <summary>
/// 把攒下的事件以 CloudEvents 信封按主题批量发布
/// </summary>
/// <remarks>
/// 没有装配消息发布者时跳过，发布失败只记日志
/// </remarks>
internal sealed partial class WorkflowEventPublisher
{
    /// <summary>
    /// 发布者
    /// </summary>
    /// <remarks>
    /// 没有为 <see langword="null"/>
    /// </remarks>
    private readonly IMessagePublisher? _publisher;

    /// <summary>
    /// 按 <c>type</c> 登记的数据模式
    /// </summary>
    /// <remarks>
    /// 应用没有登记任何模式时为 <see langword="null"/>
    /// </remarks>
    private readonly MessageDataSchemas? _schemas;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="services">这次调用的作用域：发布者与数据模式从这里取，装配了发件箱时事件写进调用方事务里的发件箱</param>
    /// <param name="logger">日志</param>
    public WorkflowEventPublisher(IServiceProvider services, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        _publisher = services.GetService<IMessagePublisher>();
        _schemas = services.GetService<MessageDataSchemas>();
        _logger = logger;
    }

    /// <summary>
    /// 应用给一个 <c>type</c> 登记的数据模式
    /// </summary>
    /// <remarks>
    /// 没有登记为 <see langword="null"/>（RFC-0009「框架事件的数据模式」，ADR-0323）；<see cref="Activities.PublishEventActivity"/> 构造自定义事件时同样用它
    /// </remarks>
    internal static string? DataSchemaOf(MessageDataSchemas? schemas, string type)
    {
        string? dataSchema = null;
        _ = schemas?.Find(type).TryGetValue(out dataSchema);
        return dataSchema;
    }

    /// <summary>
    /// 发布一组事件：按主题分组，每组一次批量发布
    /// </summary>
    public async ValueTask FlushAsync(IReadOnlyList<PendingEvent> events, CancellationToken cancellationToken)
    {
        if (_publisher is null || events.Count == 0)
        {
            return;
        }

        foreach (var group in events.GroupBy(static pending => pending.Topic, StringComparer.Ordinal))
        {
            var envelopes = group.Select(Envelope).ToList();
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
    /// 事件 → 信封
    /// </summary>
    /// <remarks>
    /// 来源 <c>/workflow</c>，类型为主题，带上这个类型登记的数据模式（ADR-0323），租户与关联进扩展属性
    /// </remarks>
    private MessageEnvelope Envelope(PendingEvent pending)
    {
        var envelope = new MessageEnvelope
        {
            Id = WorkflowIdentifiers.NewId(),
            Source = WorkflowTopics.Source,
            Type = pending.Topic,
            Time = pending.Data.At,
            Subject = pending.Subject ?? pending.Data.InstanceId,
            DataSchema = DataSchemaOf(_schemas, pending.Topic),
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
    /// 发布返回失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing workflow event '{Topic}' (envelope '{EnvelopeId}') failed with '{ErrorCode}'.")]
    private static partial void LogPublishFailed(ILogger logger, string topic, string envelopeId, string errorCode);

    /// <summary>
    /// 发布抛出异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing workflow events on topic '{Topic}' threw.")]
    private static partial void LogPublishThrew(ILogger logger, Exception exception, string topic);
}
