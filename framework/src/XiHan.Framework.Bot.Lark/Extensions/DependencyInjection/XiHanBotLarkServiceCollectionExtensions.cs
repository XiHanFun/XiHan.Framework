#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanBotLarkServiceCollectionExtensions
// Guid:daf8c664-f730-42f3-87c8-5b986773c38b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Lark.Extensions.DependencyInjection;

/// <summary>
/// 飞书 Bot 服务注册扩展
/// </summary>
public static class XiHanBotLarkServiceCollectionExtensions
{
    /// <summary>
    /// 注册飞书 Bot 提供者与配置存储
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">飞书配置委托（为空则不写入选项）</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanBotLark(this IServiceCollection services, Action<LarkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ILarkConfigStore, DefaultLarkConfigStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, LarkBotProvider>());

        return services;
    }
}
