// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.Telegram.Options;

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
