#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotRetryOptions
// Guid:d123b7fc-145d-4086-ab43-1478df623ed0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 消息发送重试配置
/// </summary>
public class TelegramBotRetryOptions
{
    /// <summary>
    /// 最大重试次数（不含首次发送）
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 退避基数毫秒（首次重试等于该值，之后每次翻倍；429 限流按 RetryAfter 精确等待）
    /// </summary>
    public int BaseDelayMs { get; set; } = 500;

    /// <summary>
    /// 退避最大延迟毫秒
    /// </summary>
    public int MaxDelayMs { get; set; } = 10_000;

    /// <summary>
    /// 最终失败后是否通知机器人管理员
    /// </summary>
    public bool NotifyAdminOnFinalFailure { get; set; } = true;
}
