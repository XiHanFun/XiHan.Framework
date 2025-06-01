#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEventBusModule
// Guid:33286838-2730-4c0a-a480-0aa34d45b21c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:34:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.BackgroundWorkers;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 曦寒框架事件总线模块
/// </summary>
[DependsOn(
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanDistributedIdsModule),
    typeof(XiHanBackgroundWorkersModule)
    )]
public class XiHanEventBusModule : XiHanModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AddEventHandlers(context.Services);
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }

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

        //services.Configure<XiHanLocalEventBusOptions>(options =>
        //{
        //    options.Handlers.AddIfNotContains(localHandlers);
        //});

        //services.Configure<XiHanDistributedEventBusOptions>(options =>
        //{
        //    options.Handlers.AddIfNotContains(distributedHandlers);
        //});
    }
}
