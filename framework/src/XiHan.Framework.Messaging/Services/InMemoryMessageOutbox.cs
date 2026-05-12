#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryMessageOutbox
// Guid:d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text.Json;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Services;

/// <summary>
/// 内存消息发件箱（线程安全）
/// </summary>
public class InMemoryMessageOutbox : IMessageOutbox
{
    private readonly ConcurrentDictionary<string, OutboxMessage> _messages = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 将消息信封加入发件箱队列
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnqueueAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var envelopeJson = JsonSerializer.Serialize(envelope, SerializerOptions);
        var outboxMessage = new OutboxMessage
        {
            MessageId = envelope.MessageId,
            EnvelopeJson = envelopeJson,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            LastError = null
        };

        _messages[envelope.MessageId] = outboxMessage;

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取待发送的消息列表
    /// </summary>
    /// <param name="batchSize">批次大小</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>待发送的发件箱消息列表</returns>
    public Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pending = _messages.Values
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    /// <summary>
    /// 标记消息为已发送
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task MarkAsSentAsync(string messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Status = OutboxMessageStatus.Sent;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 标记消息为发送失败
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="error">错误信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task MarkAsFailedAsync(string messageId, string error, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_messages.TryGetValue(messageId, out var message))
        {
            message.RetryCount++;
            message.LastError = error;

            if (message.RetryCount >= message.MaxRetries)
            {
                message.Status = OutboxMessageStatus.Failed;
            }
            else
            {
                message.Status = OutboxMessageStatus.Pending;
            }
        }

        return Task.CompletedTask;
    }
}
