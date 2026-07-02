#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotTelegramPlatformServiceCollectionExtensions
// Guid:a28c7e39-b66d-4600-8c8e-514ea5eba622
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Stores;

namespace XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;

/// <summary>
/// Telegram 机器人平台服务注册扩展
/// </summary>
public static class XiHanBotTelegramPlatformServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Telegram 多机器人平台（配置存储 / 运行时 / 处理管线 / 发送门面 / 宿主服务）
    /// </summary>
    /// <remarks>
    /// 平台默认不启用（TelegramBotSettings.Enabled = false），宿主服务空转不拉起任何机器人；
    /// 由配置或应用层设置存储开启。默认存储均以 TryAdd 注册，应用层可注册数据库/分布式实现覆盖。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configure">平台选项配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotTelegramPlatform(
        this IServiceCollection services,
        Action<TelegramBotPlatformOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            _ = services.Configure(configure);
        }

        _ = services.AddOptions<TelegramBotPlatformOptions>();
        _ = services.AddOptions<TelegramBotHandlerOptions>();

        // 配置存储（TryAdd 默认实现；应用层数据库 store 覆盖）
        services.TryAddSingleton<ITelegramBotConfigStore, DefaultTelegramBotConfigStore>();
        services.TryAddSingleton<ITelegramBotSettingsStore, DefaultTelegramBotSettingsStore>();

        // 幂等 / 会话状态 / 出站审计（TryAdd 默认实现；应用层分布式实现覆盖）
        services.TryAddSingleton<ITelegramUpdateDeduplicator, InMemoryTelegramUpdateDeduplicator>();
        services.TryAddSingleton<IConversationStateStore, InMemoryConversationStateStore>();
        services.TryAddSingleton<ITelegramMessageAuditStore, NoOpTelegramMessageAuditStore>();

        // 路由与分发管线
        services.TryAddSingleton<TelegramBotHandlerCatalog>();
        services.TryAddSingleton<TelegramCommandRouter>();
        services.TryAddSingleton<TelegramCallbackRouter>();
        services.TryAddSingleton<TelegramReplyRouter>();
        services.TryAddSingleton<TelegramMessageRouter>();
        services.TryAddSingleton<TelegramInlineQueryRouter>();
        services.TryAddSingleton<TelegramUpdateDispatcher>();

        // 发送门面
        services.TryAddSingleton<ITelegramNotifier, TelegramNotifier>();

        // 多机器人运行时与宿主服务
        services.TryAddSingleton<BotRegistry>();
        services.TryAddSingleton<TelegramBotManager>();
        _ = services.AddHostedService<TelegramBotHostedService>();

        return services;
    }

    /// <summary>
    /// 显式注册一个 Telegram 机器人处理器（同时注册 DI 瞬态与登记路由目录；平台不做程序集扫描）
    /// </summary>
    /// <typeparam name="THandler">处理器类型（须实现至少一个 IBot*Handler 接口）</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTelegramBotHandler<THandler>(this IServiceCollection services)
        where THandler : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<THandler>();
        _ = services.Configure<TelegramBotHandlerOptions>(options =>
        {
            if (!options.Handlers.Contains(typeof(THandler)))
            {
                options.Handlers.Add(typeof(THandler));
            }
        });

        return services;
    }

    /// <summary>
    /// 显式注册内置基础命令处理器（/start 欢迎、/help 可见命令列表、/myid 查看 Id）
    /// </summary>
    /// <remarks>
    /// 平台不自动注册任何处理器，需要内置命令时在应用层显式调用：
    /// <code>services.AddXiHanBotTelegramPlatform().AddTelegramBotBuiltinHandlers();</code>
    /// 三个命令均在永久放行清单内（仅豁免群组/频道白名单守卫，命令白名单仍然生效）；
    /// 回复文案可通过 <see cref="TelegramBotTexts"/>（StartReply / HelpHeader / MyIdReply）整体覆盖。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddTelegramBotBuiltinHandlers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddTelegramBotHandler<StartCommandHandler>();
        _ = services.AddTelegramBotHandler<HelpCommandHandler>();
        _ = services.AddTelegramBotHandler<MyIdCommandHandler>();

        return services;
    }
}
