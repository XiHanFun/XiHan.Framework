#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilderTelegramExtensions
// Guid:6a97d61a-08b2-4db1-b3f4-7f869e82c4e4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Extensions;

/// <summary>
/// BotBuilder Telegram 扩展
/// </summary>
public static class BotBuilderTelegramExtensions
{
    /// <summary>
    /// 启用 Telegram 提供者
    /// </summary>
    /// <param name="builder">Bot 构建器</param>
    /// <param name="configure">Telegram 配置委托</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseTelegram(this BotBuilder builder, Action<TelegramOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, TelegramBotProvider>());
        builder.Services.TryAddSingleton<ITelegramConfigStore, DefaultTelegramConfigStore>();

        return builder;
    }
}
