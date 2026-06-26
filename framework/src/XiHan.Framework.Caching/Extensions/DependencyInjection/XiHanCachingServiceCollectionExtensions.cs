#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCachingServiceCollectionExtensions
// Guid:b8dc54d7-f848-4a0b-b4c4-c2743cf6eb23
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Hybrid.Abstracts;
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
        services.AddSingleton<IDistributedCacheKeyNormalizer, DefaultDistributedCacheKeyNormalizer>();
        services.AddSingleton<IDistributedCacheSerializer, JsonDistributedCacheSerializer>();
        services.AddSingleton(typeof(IDistributedCache<>), typeof(DistributedCache<>));
        services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));

        services.AddHybridCache();
        services.AddSingleton(typeof(IHybridCache<>), typeof(XiHanHybridCache<>));
        services.AddSingleton(typeof(IHybridCache<,>), typeof(XiHanHybridCache<,>));

        // 分布式锁：默认进程内回退（仅单实例）；Redis 启用时下方替换为跨实例的 Redis 实现
        services.TryAddSingleton<IDistributedLock, InMemoryDistributedLock>();

        services.Configure<XiHanDistributedCacheOptions>(cacheOptions =>
        {
            cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(20);
        });

        // 配置 Redis 缓存选项
        services.Configure<XiHanRedisCacheOptions>(configuration.GetSection(XiHanRedisCacheOptions.SectionName));

        // 获取 Redis 配置
        var redisOptions = configuration.GetSection(XiHanRedisCacheOptions.SectionName).Get<XiHanRedisCacheOptions>() ?? new XiHanRedisCacheOptions();

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

        // 暴露原生 Redis 连接 + 泛型队列：IConnectionMultiplexer 为长寿命单例（与 XiHanRedisCache 各连各的）。
        // IRedisStreamQueue<>（Streams 可靠消息队列：消费组+ACK+重投）、IRedisDelayQueue<>（Sorted Set 延迟队列）均为开放泛型，按封闭类型注入。
        if (!redisOptions.Configuration.IsNullOrEmpty())
        {
            services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions.Configuration));
            services.TryAddSingleton(typeof(IRedisStreamQueue<>), typeof(RedisStreamQueue<>));
            services.TryAddSingleton(typeof(IRedisDelayQueue<>), typeof(RedisDelayQueue<>));
            // 分布式锁升级为 Redis 跨实例实现（替换上面的进程内回退）
            services.Replace(ServiceDescriptor.Singleton<IDistributedLock, RedisDistributedLock>());
        }

        return services;
    }
}
