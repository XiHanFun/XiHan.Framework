#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MessageSendResult
// Guid:8ec5ca6b-cf04-4a22-8bd7-e1846f0d15ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 15:01:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
