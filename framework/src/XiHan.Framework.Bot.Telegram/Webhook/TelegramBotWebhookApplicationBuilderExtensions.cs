#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotWebhookApplicationBuilderExtensions
// Guid:c38e1148-fa50-4a20-8960-0ab6594ffb61
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Builder;

namespace XiHan.Framework.Bot.Telegram.Webhook;

/// <summary>
/// Telegram Bot Webhook 中间件注册扩展
/// </summary>
public static class TelegramBotWebhookApplicationBuilderExtensions
{
    /// <summary>
    /// 启用 Telegram Bot Webhook 接收中间件（匹配 POST {prefix}/{botName}；仅 Webhook 传输模式需要）
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseTelegramBotWebhook(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<TelegramBotWebhookMiddleware>();
    }
}
