#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCachingServiceCollectionExtensions
// Guid:6f62e6f6-d4e0-46a0-a7cc-cdbb69489ba9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Options;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Caching.Extensions.DependencyInjection;

/// <summary>
/// 曦寒缓存服务集合扩展
/// </summary>
public static class XiHanCachingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒缓存服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddSingleton(typeof(IDistributedCache<>), typeof(DistributedCache<>));
        services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));

        services.AddHybridCache();
        services.AddSingleton(typeof(IHybridCache<>), typeof(XiHanHybridCache<>));
        services.AddSingleton(typeof(IHybridCache<,>), typeof(XiHanHybridCache<,>));

        services.Configure<XiHanDistributedCacheOptions>(cacheOptions =>
        {
            cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(20);
        });

        // 配置 Redis 缓存选项
        services.Configure<XiHanRedisCacheOptions>(configuration.GetSection(XiHanRedisCacheOptions.SectionName));

        // 获取 Redis 配置
        var redisOptions = new XiHanRedisCacheOptions();
        configuration.GetSection(XiHanRedisCacheOptions.SectionName).Bind(redisOptions);

        if (!redisOptions.IsEnabled)
        {
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            if (!redisOptions.Configuration.IsNullOrEmpty())
            {
                options.Configuration = redisOptions.Configuration;
            }

            if (!redisOptions.InstanceName.IsNullOrEmpty())
            {
                options.InstanceName = redisOptions.InstanceName;
            }
        });

        services.Replace(ServiceDescriptor.Singleton<IDistributedCache, XiHanRedisCache>());

        return services;
    }
}
