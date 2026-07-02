#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotPlatformConsts
// Guid:69272fb5-bbf3-4db1-aa30-2d765dcff3db
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Options;

/// <summary>
/// Telegram 机器人平台常量
/// </summary>
public static class TelegramBotPlatformConsts
{
    /// <summary>
    /// 默认 Webhook 路由前缀
    /// </summary>
    public const string DefaultWebhookRoutePrefix = "/api/telegram-bot/webhook";

    /// <summary>
    /// Telegram Webhook 密钥令牌请求头名称
    /// </summary>
    public const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    /// <summary>
    /// 回调数据 Action 与 Id 的分隔符（约定 callback data 形如 action:id）
    /// </summary>
    public const char CallbackDataSeparator = ':';

    /// <summary>
    /// 深链 /start 命令
    /// </summary>
    public const string StartCommand = "/start";
}
