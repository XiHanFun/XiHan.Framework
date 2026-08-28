// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Stores;

namespace XiHan.Framework.Bot.WeCom.Extensions.DependencyInjection;

/// <summary>
/// 企业微信 Bot 服务注册扩展
/// </summary>
public static class XiHanBotWeComServiceCollectionExtensions
{
    /// <summary>
    /// 注册企业微信 Bot 提供者与配置存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">企业微信配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotWeCom(this IServiceCollection services, Action<WeComOptions>? configure = null)
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

        services.TryAddSingleton<IWeComConfigStore, DefaultWeComConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, WeComBotProvider>());

        return services;
    }
}
