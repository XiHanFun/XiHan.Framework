#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotTelegramServiceCollectionExtensions
// Guid:ed80c650-218a-4151-928d-1f91fae6c99c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;

/// <summary>
/// Telegram Bot 服务注册扩展
/// </summary>
public static class XiHanBotTelegramServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Telegram Bot 提供者与配置存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">Telegram 配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotTelegram(this IServiceCollection services, Action<TelegramOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ITelegramConfigStore, DefaultTelegramConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, TelegramBotProvider>());

        return services;
    }
}
