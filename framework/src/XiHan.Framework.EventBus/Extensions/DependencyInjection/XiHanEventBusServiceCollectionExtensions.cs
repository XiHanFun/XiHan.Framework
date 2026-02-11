#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEventBusServiceCollectionExtensions
// Guid:33286838-2730-4c0a-a480-0aa34d45b21c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.EventBus.Extensions.DependencyInjection;

/// <summary>
/// 曦寒事件总线服务集合扩展
/// </summary>
public static class XiHanEventBusServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒事件总线服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        AddEventHandlers(services);
        return services;
    }

    /// <summary>
    /// 添加事件处理器
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void AddEventHandlers(IServiceCollection services)
    {
        var localHandlers = new List<Type>();
        var distributedHandlers = new List<Type>();

        services.OnRegistered(context =>
        {
            if (context.ImplementationType.IsAssignableToGeneric(typeof(ILocalEventHandler<>)))
            {
                localHandlers.Add(context.ImplementationType);
            }

            if (context.ImplementationType.IsAssignableToGeneric(typeof(IDistributedEventHandler<>)))
            {
                distributedHandlers.Add(context.ImplementationType);
            }
        });

        services.Configure<XiHanLocalEventBusOptions>(options =>
        {
            options.Handlers.AddIfNotContains(localHandlers);
        });

        services.Configure<XiHanDistributedEventBusOptions>(options =>
        {
            options.Handlers.AddIfNotContains(distributedHandlers);
        });
    }
}
