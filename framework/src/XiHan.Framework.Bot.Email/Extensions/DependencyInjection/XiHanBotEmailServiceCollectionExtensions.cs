// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Stores;

namespace XiHan.Framework.Bot.Email.Extensions.DependencyInjection;

/// <summary>
/// 邮件 Bot 服务注册扩展
/// </summary>
public static class XiHanBotEmailServiceCollectionExtensions
{
    /// <summary>
    /// 注册邮件 Bot 提供者与配置存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">邮件配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotEmail(this IServiceCollection services, Action<EmailOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IEmailConfigStore, DefaultEmailConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, EmailBotProvider>());

        return services;
    }
}
