// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Events;

/// <summary>
/// 把调度事件发成 CloudEvents 信封
/// </summary>
/// <remarks>
/// 没有发布者时跳过，发布失败只记日志（事件是旁路，不影响调度状态）
/// </remarks>
internal sealed partial class SchedulingEventPublisher
{
    /// <summary>
    /// 发布者
    /// </summary>
    /// <remarks>
    /// 没有装配消息能力时为空
    /// </remarks>
    private readonly IMessagePublisher? _publisher;

    /// <summary>
    /// 按 <c>type</c> 登记的数据模式
    /// </summary>
    /// <remarks>
    /// 应用没有登记任何模式时为空
    /// </remarks>
    private readonly MessageDataSchemas? _schemas;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="publisher">发布者；没有装配消息能力时为 <see langword="null"/></param>
    /// <param name="schemas">按 <c>type</c> 登记的数据模式；没有时为 <see langword="null"/></param>
    /// <param name="logger">日志</param>
    public SchedulingEventPublisher(IMessagePublisher? publisher, MessageDataSchemas? schemas, ILogger logger)
    {
        _publisher = publisher;
        _schemas = schemas;
        _logger = logger;
    }

    /// <summary>
    /// 是否会发布
    /// </summary>
    public bool IsEnabled => _publisher is not null;

    /// <summary>
    /// 发布一个事件
    /// </summary>
    public async ValueTask PublishAsync(string topic, SchedulingEventData data, CancellationToken cancellationToken)
    {
        if (_publisher is null)
        {
            return;
        }

        // 主题就是 type：应用给它登记了数据模式才带上 dataschema（RFC-0009「框架事件的数据模式」，ADR-0323）
        string? dataSchema = null;
        _ = _schemas?.Find(topic).TryGetValue(out dataSchema);
        var envelope = new MessageEnvelope
        {
            Id = JobRun.NewId(),
            Source = SchedulingTopics.Source,
            Type = topic,
            Time = data.At,
            Subject = data.RunId ?? data.Job,
            DataSchema = dataSchema,
            Data = JsonSerializer.SerializeToUtf8Bytes(data, SchedulingJsonContext.Default.SchedulingEventData),
        };
        try
        {
            var result = await _publisher.PublishAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                LogPublishFailed(_logger, topic, result.Error!.Code);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPublishThrew(_logger, exception, topic);
        }
    }

    /// <summary>
    /// 发布失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing scheduling event '{Topic}' failed with '{ErrorCode}'.")]
    private static partial void LogPublishFailed(ILogger logger, string topic, string errorCode);

    /// <summary>
    /// 发布抛出
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing scheduling event '{Topic}' threw.")]
    private static partial void LogPublishThrew(ILogger logger, Exception exception, string topic);
}
