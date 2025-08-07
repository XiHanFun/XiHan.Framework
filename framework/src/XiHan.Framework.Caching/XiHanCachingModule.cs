#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCachingModule
// Guid:6f62e6f6-d4e0-46a0-a7cc-cdbb69489ba9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:29:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Caching;

/// <summary>
/// 曦寒框架缓存模块
/// </summary>
public class XiHanCachingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        _ = services.AddMemoryCache();
        _ = services.AddDistributedMemoryCache();
        _ = services.AddSingleton(typeof(IDistributedCache<>), typeof(DistributedCache<>));
        _ = services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));

        _ = services.AddHybridCache();
        _ = services.AddSingleton(typeof(IHybridCache<>), typeof(XiHanHybridCache<>));
        _ = services.AddSingleton(typeof(IHybridCache<,>), typeof(XiHanHybridCache<,>));

        services.Configure<XiHanDistributedCacheOptions>(cacheOptions =>
        {
            cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(20);
        });

        var configuration = context.Services.GetConfiguration();

        var redisEnabled = configuration["Redis:IsEnabled"];
        if (!string.IsNullOrEmpty(redisEnabled) && !bool.Parse(redisEnabled))
        {
            return;
        }

        _ = context.Services.AddStackExchangeRedisCache(options =>
        {
            var redisConfiguration = configuration["Redis:Configuration"];
            if (!redisConfiguration.IsNullOrEmpty())
            {
                options.Configuration = redisConfiguration;
            }
        });

        _ = context.Services.Replace(ServiceDescriptor.Singleton<IDistributedCache, XiHanRedisCache>());
    }
}
