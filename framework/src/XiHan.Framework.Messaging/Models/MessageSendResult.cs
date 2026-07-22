// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Messaging.Models;

/// <summary>
/// 消息发送结果
/// </summary>
public class MessageSendResult
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// 通道
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// 接收地址
    /// </summary>
    public string RecipientAddress { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 第三方消息ID
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// 分发时间
    /// </summary>
    public DateTimeOffset DispatchedAt { get; set; } = DateTimeOffset.UtcNow;
}
