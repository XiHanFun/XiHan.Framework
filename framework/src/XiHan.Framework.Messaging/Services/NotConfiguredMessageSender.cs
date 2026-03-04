#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NotConfiguredMessageSender
// Guid:1be4dc40-152f-416e-bfe0-41fc2da4c1ec
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 15:07:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Services;

/// <summary>
/// 未配置发送器时的兜底实现
/// </summary>
public class NotConfiguredMessageSender : IMessageSender
{
    /// <summary>
    /// 任意通道都可匹配，用于提供明确失败结果
    /// </summary>
    /// <param name="channel">消息通道</param>
    /// <returns>始终返回 true</returns>
    public bool CanHandle(string channel)
    {
        return true;
    }

    /// <summary>
    /// 返回“未配置发送器”的失败结果
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="recipient">接收人</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>失败结果</returns>
    public Task<MessageSendResult> SendAsync(MessageEnvelope envelope, MessageRecipient recipient, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MessageSendResult
        {
            MessageId = envelope.MessageId,
            Channel = envelope.Channel,
            RecipientAddress = recipient.Address,
            IsSuccess = false,
            ErrorMessage = $"消息通道 '{envelope.Channel}' 未配置发送器"
        });
    }
}
