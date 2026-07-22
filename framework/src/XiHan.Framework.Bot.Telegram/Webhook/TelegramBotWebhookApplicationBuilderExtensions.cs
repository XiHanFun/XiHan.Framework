// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
