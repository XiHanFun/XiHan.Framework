#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotDingTalkServiceCollectionExtensions
// Guid:e764fa15-5f00-4b51-806f-3d0984fc3c7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.DingTalk.Extensions.DependencyInjection;

/// <summary>
/// 钉钉 Bot 服务注册扩展
/// </summary>
public static class XiHanBotDingTalkServiceCollectionExtensions
{
    /// <summary>
    /// 注册钉钉 Bot 提供者与配置存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">钉钉配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotDingTalk(this IServiceCollection services, Action<DingTalkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IDingTalkConfigStore, DefaultDingTalkConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, DingTalkBotProvider>());

        return services;
    }
}
