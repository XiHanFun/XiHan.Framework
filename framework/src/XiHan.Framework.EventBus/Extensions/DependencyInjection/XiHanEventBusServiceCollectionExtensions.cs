#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEventBusServiceCollectionExtensions
// Guid:00efaecf-ecf9-4204-900b-bf1f21c61fa1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
        services.Configure<EventBoxProcessingOptions>(
            configuration.GetSection(EventBoxProcessingOptions.SectionName));
        ConfigureDefaultEventBoxes(services);
        RegisterDefaultEventBoxServices(services);
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

    /// <summary>
    /// 配置默认事件盒
    /// </summary>
    /// <param name="services"></param>
    private static void ConfigureDefaultEventBoxes(IServiceCollection services)
    {
        services.Configure<XiHanDistributedEventBusOptions>(options =>
        {
            options.Outboxes.Configure(config =>
            {
                if (config.ImplementationType == default)
                {
                    config.ImplementationType = typeof(InMemoryEventOutbox);
                }

                if (string.IsNullOrWhiteSpace(config.DatabaseName))
                {
                    config.DatabaseName = "Default";
                }
            });

            options.Inboxes.Configure(config =>
            {
                if (config.ImplementationType == default)
                {
                    config.ImplementationType = typeof(InMemoryEventInbox);
                }

                if (string.IsNullOrWhiteSpace(config.DatabaseName))
                {
                    config.DatabaseName = "Default";
                }
            });
        });
    }

    /// <summary>
    /// 注册默认事件盒服务
    /// </summary>
    /// <param name="services"></param>
    private static void RegisterDefaultEventBoxServices(IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryEventOutbox>();
        services.TryAddSingleton<InMemoryEventInbox>();
        services.TryAddSingleton<IEventOutbox>(sp => sp.GetRequiredService<InMemoryEventOutbox>());
        services.TryAddSingleton<IEventInbox>(sp => sp.GetRequiredService<InMemoryEventInbox>());

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventBoxOutboxSenderHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EventBoxInboxProcessorHostedService>());
    }
}
