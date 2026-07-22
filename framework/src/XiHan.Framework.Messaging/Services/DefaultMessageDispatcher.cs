// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;
using XiHan.Framework.Messaging.Options;

namespace XiHan.Framework.Messaging.Services;

/// <summary>
/// 默认消息调度器
/// </summary>
public class DefaultMessageDispatcher : IMessageDispatcher
{
    private readonly IReadOnlyList<IMessageSender> _senders;
    private readonly XiHanMessagingOptions _options;
    private readonly ILogger<DefaultMessageDispatcher> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultMessageDispatcher(
        IEnumerable<IMessageSender> senders,
        IOptions<XiHanMessagingOptions> options,
        ILogger<DefaultMessageDispatcher> logger)
    {
        _senders = senders.ToArray();
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 分发消息到指定通道
    /// </summary>
    public async Task<IReadOnlyList<MessageSendResult>> DispatchAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(envelope.Channel))
        {
            throw new InvalidOperationException("消息通道不能为空");
        }

        if (envelope.Recipients.Count == 0)
        {
            return [];
        }

        // 优先真实发送器；NotConfiguredMessageSender 的 CanHandle 恒为 true，只能作「无可用发送器」的兜底，不可遮蔽真实发送器
        var channel = envelope.Channel.Trim();
        var sender = _senders.FirstOrDefault(item => item is not NotConfiguredMessageSender && item.CanHandle(channel))
            ?? _senders.FirstOrDefault(item => item.CanHandle(channel));
        if (sender is null)
        {
            var error = $"未找到可用发送器，通道: {envelope.Channel}";
            _logger.LogWarning("{Error}", error);
            if (_options.ThrowWhenNoSender)
            {
                throw new InvalidOperationException(error);
            }

            return envelope.Recipients.Select(recipient => new MessageSendResult
            {
                MessageId = envelope.MessageId,
                Channel = envelope.Channel,
                RecipientAddress = recipient.Address,
                IsSuccess = false,
                ErrorMessage = error
            }).ToArray();
        }

        var results = new List<MessageSendResult>(envelope.Recipients.Count);
        foreach (var recipient in envelope.Recipients)
        {
            try
            {
                var result = await sender.SendAsync(envelope, recipient, cancellationToken);
                result.MessageId = string.IsNullOrWhiteSpace(result.MessageId) ? envelope.MessageId : result.MessageId;
                result.Channel = string.IsNullOrWhiteSpace(result.Channel) ? envelope.Channel : result.Channel;
                result.RecipientAddress = string.IsNullOrWhiteSpace(result.RecipientAddress) ? recipient.Address : result.RecipientAddress;
                results.Add(result);

                if (!result.IsSuccess && !_options.ContinueOnError)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息发送异常，通道: {Channel}, 地址: {Address}", envelope.Channel, recipient.Address);
                results.Add(new MessageSendResult
                {
                    MessageId = envelope.MessageId,
                    Channel = envelope.Channel,
                    RecipientAddress = recipient.Address,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                });

                if (!_options.ContinueOnError)
                {
                    break;
                }
            }
        }

        return results;
    }
}
