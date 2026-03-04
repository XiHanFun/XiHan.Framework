#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IMessageSender
// Guid:d4924c31-0bb2-48ce-84b8-7cf462f84251
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 14:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
