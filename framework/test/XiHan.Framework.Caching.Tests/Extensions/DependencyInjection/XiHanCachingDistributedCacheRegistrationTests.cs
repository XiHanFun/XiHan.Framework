// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Options;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 分布式缓存注册唯一性测试
/// </summary>
/// <remarks>
/// AddStackExchangeRedisCache 内部是用 services.Add（非 TryAdd）追加 RedisCache 的，
/// 而 ServiceCollection.Replace 只移除第一条匹配项，结果集合里会同时留下 RedisCache 与 XiHanRedisCache。
/// 按最后一条生效时单条注入拿到的确实是 XiHanRedisCache，但 GetServices&lt;IDistributedCache&gt;()
/// 会多出一个游离的 RedisCache，任何枚举全部注册的代码都会额外建一个 Redis 客户端。
/// 这里在服务描述符层面锁死「该服务类型只剩一条注册」，不解析服务以免用例真的去连 Redis。
/// </remarks>
public class XiHanCachingDistributedCacheRegistrationTests
{
    /// <summary>
    /// Redis 启用时分布式缓存只保留一条注册，且实现为曦寒 Redis 缓存
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisEnabled_RegistersExactlyOneDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(true));

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedCache));

        Assert.Equal(typeof(XiHanRedisCache), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// Redis 启用时不留下底层扩展追加的原生 RedisCache 注册
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisEnabled_DropsNativeRedisCacheRegistration()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(true));

        Assert.DoesNotContain(
            services,
            item => item.ServiceType == typeof(IDistributedCache) && item.ImplementationType?.Name == "RedisCache");
    }

    /// <summary>
    /// Redis 未启用时分布式缓存同样只保留一条注册
    /// </summary>
    /// <remarks>
    /// 反例：确认清空重注册没有波及未启用 Redis 的默认装配路径。
    /// </remarks>
    [Fact]
    public void AddXiHanCaching_WhenRedisDisabled_RegistersExactlyOneDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedCache));

        Assert.Equal("MemoryDistributedCache", descriptor.ImplementationType?.Name);
    }

    /// <summary>
    /// 构建 Redis 开关配置
    /// </summary>
    /// <param name="redisEnabled">是否启用 Redis</param>
    /// <returns>配置</returns>
    private static IConfiguration BuildConfiguration(bool redisEnabled)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{XiHanRedisCacheOptions.SectionName}:IsEnabled"] = redisEnabled ? "true" : "false"
        };

        if (redisEnabled)
        {
            settings[$"{XiHanRedisCacheOptions.SectionName}:Configuration"] = "localhost:6379";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
