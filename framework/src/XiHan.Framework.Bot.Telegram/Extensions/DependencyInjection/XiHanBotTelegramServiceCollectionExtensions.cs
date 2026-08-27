// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        // 本方法登记的配置存储依赖 IOptionsMonitor<T>，必须自己保证选项基础设施在场：
        // 不传 configure 时不会走到 services.Configure(...)，选项服务就没人登记，
        // 调用方 AddXiHanBotXxx() 之后 BuildServiceProvider 会直接抛「无法解析 IOptionsMonitor<T>」。
        // AddOptions() 内部全是 TryAdd，重复调用幂等，放这里没有副作用。
        services.AddOptions();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ITelegramConfigStore, DefaultTelegramConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, TelegramBotProvider>());

        return services;
    }
}
