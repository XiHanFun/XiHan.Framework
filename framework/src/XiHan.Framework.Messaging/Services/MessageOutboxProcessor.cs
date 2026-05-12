#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MessageOutboxProcessor
// Guid:e5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;
using XiHan.Framework.Messaging.Options;

namespace XiHan.Framework.Messaging.Services;

/// <summary>
/// 消息发件箱处理器
/// </summary>
public class MessageOutboxProcessor : IMessageOutboxProcessor
{
    private readonly IMessageOutbox _outbox;
    private readonly IMessageDispatcher _dispatcher;
    private readonly XiHanMessagingOptions _options;
    private readonly ILogger<MessageOutboxProcessor> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 构造函数
    /// </summary>
    public MessageOutboxProcessor(
        IMessageOutbox outbox,
        IMessageDispatcher dispatcher,
        IOptions<XiHanMessagingOptions> options,
        ILogger<MessageOutboxProcessor> logger)
    {
        _outbox = outbox;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 处理发件箱中的待发送消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var pendingMessages = await _outbox.GetPendingMessagesAsync(_options.OutboxBatchSize, cancellationToken);

            if (pendingMessages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("发件箱待处理消息数: {Count}", pendingMessages.Count);

            foreach (var outboxMessage in pendingMessages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var envelope = JsonSerializer.Deserialize<MessageEnvelope>(outboxMessage.EnvelopeJson, SerializerOptions);
                    if (envelope is null)
                    {
                        await _outbox.MarkAsFailedAsync(outboxMessage.MessageId, "消息信封反序列化失败", cancellationToken);
                        continue;
                    }

                    var results = await _dispatcher.DispatchAsync(envelope, cancellationToken);

                    var hasFailed = results.Any(result => !result.IsSuccess);
                    if (hasFailed)
                    {
                        var errors = results
                            .Where(result => !result.IsSuccess)
                            .Select(result => result.ErrorMessage)
                            .Where(error => error is not null);

                        var combinedError = string.Join("; ", errors);
                        await _outbox.MarkAsFailedAsync(outboxMessage.MessageId, combinedError, cancellationToken);

                        _logger.LogWarning("发件箱消息发送部分失败，消息ID: {MessageId}, 错误: {Error}",
                            outboxMessage.MessageId, combinedError);
                    }
                    else
                    {
                        await _outbox.MarkAsSentAsync(outboxMessage.MessageId, cancellationToken);

                        _logger.LogDebug("发件箱消息发送成功，消息ID: {MessageId}", outboxMessage.MessageId);
                    }
                }
                catch (Exception ex)
                {
                    await _outbox.MarkAsFailedAsync(outboxMessage.MessageId, ex.Message, cancellationToken);

                    _logger.LogError(ex, "发件箱消息处理异常，消息ID: {MessageId}", outboxMessage.MessageId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发件箱处理器运行异常");
        }
    }
}
