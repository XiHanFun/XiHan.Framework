// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Caching.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Hybrid.Abstracts;
using XiHan.Framework.Caching.Options;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 曦寒缓存服务注册扩展测试
/// </summary>
/// <remarks>
/// 注册表是整个缓存模块的装配契约：开放泛型的映射、单例生命周期、以及 Redis 开关下的实现替换。
/// 全部断言都停在服务描述符层面或只解析不触网的服务，避免用例真的去连 Redis。
/// </remarks>
public class XiHanCachingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 扩展方法返回同一个服务集合，便于链式调用
    /// </summary>
    [Fact]
    public void AddXiHanCaching_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanCaching(BuildConfiguration(false)));
    }

    /// <summary>
    /// 键规范化器与序列化器注册为单例的默认实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_RegistersKeyNormalizerAndSerializerAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        var normalizer = Assert.Single(services, item => item.ServiceType == typeof(IDistributedCacheKeyNormalizer));
        var serializer = Assert.Single(services, item => item.ServiceType == typeof(IDistributedCacheSerializer));

        Assert.Equal(typeof(DefaultDistributedCacheKeyNormalizer), normalizer.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, normalizer.Lifetime);
        Assert.Equal(typeof(JsonDistributedCacheSerializer), serializer.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, serializer.Lifetime);
    }

    /// <summary>
    /// 分布式缓存与混合缓存都按开放泛型注册
    /// </summary>
    /// <remarks>
    /// 开放泛型注册意味着任意缓存项类型都能直接注入，漏掉任何一条都会在运行期才暴露成解析失败。
    /// </remarks>
    [Theory]
    [InlineData(typeof(IDistributedCache<>), typeof(DistributedCache<>))]
    [InlineData(typeof(IDistributedCache<,>), typeof(DistributedCache<,>))]
    [InlineData(typeof(IHybridCache<>), typeof(XiHanHybridCache<>))]
    [InlineData(typeof(IHybridCache<,>), typeof(XiHanHybridCache<,>))]
    public void AddXiHanCaching_RegistersOpenGenericCaches(Type serviceType, Type implementationType)
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// Redis 未启用时分布式缓存落到内存实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisDisabled_UsesInMemoryDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedCache));

        Assert.Equal("MemoryDistributedCache", descriptor.ImplementationType?.Name);
    }

    /// <summary>
    /// Redis 未启用时分布式锁落到进程内回退实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisDisabled_UsesInMemoryDistributedLock()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedLock));

        Assert.Equal(typeof(InMemoryDistributedLock), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// Redis 未启用时不注册任何原生 Redis 依赖
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisDisabled_RegistersNoRedisDependencies()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConnectionMultiplexer));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IRedisStreamQueue<>));
    }

    /// <summary>
    /// 全局缓存条目选项默认二十分钟滑动过期
    /// </summary>
    /// <remarks>
    /// 这是没有显式配置时所有缓存项的兜底存活时长，属于对外承诺的默认口径。
    /// </remarks>
    [Fact]
    public void AddXiHanCaching_SetsDefaultSlidingExpiration()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanDistributedCacheOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(20), options.GlobalCacheEntryOptions.SlidingExpiration);
    }

    /// <summary>
    /// Redis 选项从配置节绑定进容器
    /// </summary>
    [Fact]
    public void AddXiHanCaching_BindsRedisOptionsFromConfiguration()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanRedisCacheOptions.SectionName}:IsEnabled"] = "false",
                [$"{XiHanRedisCacheOptions.SectionName}:InstanceName"] = "xihan"
            })
            .Build());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanRedisCacheOptions>>().Value;

        Assert.False(options.IsEnabled);
        Assert.Equal("xihan", options.InstanceName);
    }

    /// <summary>
    /// 键规范化器可从容器解析，且在无租户上下文时给出无租户前缀
    /// </summary>
    [Fact]
    public void AddXiHanCaching_ResolvesKeyNormalizerWorkingWithoutTenantAccessor()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));
        using var provider = services.BuildServiceProvider();

        var normalizer = provider.GetRequiredService<IDistributedCacheKeyNormalizer>();

        Assert.Equal("0:orders:k1", normalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs("k1", "orders", false)));
    }

    /// <summary>
    /// 序列化器可从容器解析
    /// </summary>
    [Fact]
    public void AddXiHanCaching_ResolvesSerializer()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(false));
        using var provider = services.BuildServiceProvider();

        Assert.IsType<JsonDistributedCacheSerializer>(provider.GetRequiredService<IDistributedCacheSerializer>());
    }

    /// <summary>
    /// Redis 启用时分布式缓存的最终实现被换成曦寒 Redis 缓存
    /// </summary>
    /// <remarks>
    /// 只断言最终生效的那条注册，不解析服务，避免用例真的去建立 Redis 连接。
    /// </remarks>
    [Fact]
    public void AddXiHanCaching_WhenRedisEnabled_ResolvesToXiHanRedisCache()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(true));

        var descriptor = services.Last(item => item.ServiceType == typeof(IDistributedCache));

        Assert.Equal(typeof(XiHanRedisCache), descriptor.ImplementationType);
    }

    /// <summary>
    /// Redis 启用且配置了连接串时，分布式锁与 Stream 队列升级为跨实例实现
    /// </summary>
    [Fact]
    public void AddXiHanCaching_WhenRedisEnabledWithConnectionString_UpgradesLockAndStreamQueue()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(BuildConfiguration(true));

        var lockDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedLock));
        var streamDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IRedisStreamQueue<>));

        Assert.Equal(typeof(RedisDistributedLock), lockDescriptor.ImplementationType);
        Assert.Equal(typeof(RedisStreamQueue<>), streamDescriptor.ImplementationType);
        Assert.Contains(services, item => item.ServiceType == typeof(IConnectionMultiplexer));
    }

    /// <summary>
    /// Redis 启用但没有连接串时，跨实例组件保持进程内回退
    /// </summary>
    /// <remarks>
    /// 没有连接串就建不了连接，此时升级成 Redis 实现只会在解析时炸掉，保持回退才是安全行为。
    /// </remarks>
    [Fact]
    public void AddXiHanCaching_WhenRedisEnabledWithoutConnectionString_KeepsInProcessFallbacks()
    {
        var services = new ServiceCollection();
        services.AddXiHanCaching(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{XiHanRedisCacheOptions.SectionName}:IsEnabled"] = "true"
            })
            .Build());

        var lockDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedLock));

        Assert.Equal(typeof(InMemoryDistributedLock), lockDescriptor.ImplementationType);
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConnectionMultiplexer));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IRedisStreamQueue<>));
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
