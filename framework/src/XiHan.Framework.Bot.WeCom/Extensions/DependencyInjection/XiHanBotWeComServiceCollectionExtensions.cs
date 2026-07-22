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

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IWeComConfigStore, DefaultWeComConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, WeComBotProvider>());

        return services;
    }
}
