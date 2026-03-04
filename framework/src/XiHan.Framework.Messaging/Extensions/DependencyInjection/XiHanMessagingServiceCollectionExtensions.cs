#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanMessagingServiceCollectionExtensions
// Guid:3f8acc86-4291-415f-b3ef-37f17a31de6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/04 15:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Options;
using XiHan.Framework.Messaging.Services;

namespace XiHan.Framework.Messaging.Extensions.DependencyInjection;

/// <summary>
/// 消息服务注册扩展
/// </summary>
public static class XiHanMessagingServiceCollectionExtensions
{
    /// <summary>
    /// 注册消息模块核心服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置回调</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanMessaging(
        this IServiceCollection services,
        Action<XiHanMessagingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<XiHanMessagingOptions>(_ => { });
        }

        services.TryAddSingleton<IMessageDispatcher, DefaultMessageDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageSender, NotConfiguredMessageSender>());

        return services;
    }
}
