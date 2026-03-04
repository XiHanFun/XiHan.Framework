#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MessageRecipient
// Guid:9f912325-9d98-4efe-88f0-27933736286b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
