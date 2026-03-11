#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramOptions
// Guid:ac09421e-d20c-4c90-8e1b-3510ebc0f04e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:49:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Providers.Telegram;

/// <summary>
/// Telegram 提供者配置
/// </summary>
public class TelegramOptions
{
    /// <summary>
    /// 是否启用该提供者
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Bot 令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 默认会话 ID 或用户名
    /// </summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// 默认解析模式
    /// </summary>
    public string? ParseMode { get; set; }

    /// <summary>
    /// 是否禁用通知
    /// </summary>
    public bool DisableNotification { get; set; } = false;
}
