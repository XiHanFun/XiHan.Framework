// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Messaging.Models;

/// <summary>
/// 消息接收人
/// </summary>
public class MessageRecipient
{
    /// <summary>
    /// 接收人ID
    /// </summary>
    public string? ReceiverId { get; set; }

    /// <summary>
    /// 接收地址（邮箱/手机号/用户ID）
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 接收人名称
    /// </summary>
    public string? DisplayName { get; set; }
}
