// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 消息发送器
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// 是否支持指定通道
    /// </summary>
    /// <param name="channel">消息通道</param>
    /// <returns>是否支持</returns>
    bool CanHandle(string channel);

    /// <summary>
    /// 发送单条消息
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="recipient">接收人</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发送结果</returns>
    Task<MessageSendResult> SendAsync(MessageEnvelope envelope, MessageRecipient recipient, CancellationToken cancellationToken = default);
}
