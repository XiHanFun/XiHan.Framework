// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Events;

/// <summary>
/// 把调度事件发成 CloudEvents 信封。
/// </summary>
/// <remarks>
/// 没有发布者时跳过，发布失败只记日志（事件是旁路，不影响调度状态）。
/// </remarks>
internal sealed partial class SchedulingEventPublisher
{
    /// <summary>
    /// 发布者。
    /// </summary>
    /// <remarks>
    /// 没有装配消息能力时为空。
    /// </remarks>
    private readonly IMessagePublisher? _publisher;

    /// <summary>
    /// 日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 创建。
    /// </summary>
    public SchedulingEventPublisher(IMessagePublisher? publisher, ILogger logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// 是否会发布。
    /// </summary>
    public bool IsEnabled => _publisher is not null;

    /// <summary>
    /// 发布一个事件。
    /// </summary>
    public async ValueTask PublishAsync(string topic, SchedulingEventData data, CancellationToken cancellationToken)
    {
        if (_publisher is null)
        {
            return;
        }

        var envelope = new MessageEnvelope
        {
            Id = JobRun.NewId(),
            Source = SchedulingTopics.Source,
            Type = topic,
            Time = data.At,
            Subject = data.RunId ?? data.Job,
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
    /// 发布失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing scheduling event '{Topic}' failed with '{ErrorCode}'.")]
    private static partial void LogPublishFailed(ILogger logger, string topic, string errorCode);

    /// <summary>
    /// 发布抛出。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing scheduling event '{Topic}' threw.")]
    private static partial void LogPublishThrew(ILogger logger, Exception exception, string topic);
}
