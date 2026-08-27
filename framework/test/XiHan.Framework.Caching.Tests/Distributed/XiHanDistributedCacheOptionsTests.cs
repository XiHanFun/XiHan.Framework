// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using XiHan.Framework.Caching.Distributed;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 曦寒分布式缓存选项测试
/// </summary>
/// <remarks>
/// 覆盖默认值语义与三个 ConfigureCache 重载的收敛点：无论按泛型、按类型还是按名称登记，
/// 最终都要落到同一个「按缓存名匹配」的配置器上，缓存名的推导规则与 CacheNameAttribute 保持一致。
/// </remarks>
public class XiHanDistributedCacheOptionsTests
{
    /// <summary>
    /// 新建选项的默认值
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var options = new XiHanDistributedCacheOptions();

        Assert.True(options.HideErrors);
        Assert.Equal(string.Empty, options.KeyPrefix);
        Assert.Empty(options.CacheConfigurators);
        Assert.NotNull(options.GlobalCacheEntryOptions);
        Assert.Null(options.GlobalCacheEntryOptions.SlidingExpiration);
        Assert.Null(options.GlobalCacheEntryOptions.AbsoluteExpiration);
        Assert.Null(options.GlobalCacheEntryOptions.AbsoluteExpirationRelativeToNow);
    }

    /// <summary>
    /// 按名称登记的配置器只对同名缓存生效
    /// </summary>
    [Fact]
    public void ConfigureCache_ByName_MatchesOnlyThatCacheName()
    {
        var entryOptions = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanDistributedCacheOptions();

        options.ConfigureCache("orders", entryOptions);

        var configurator = Assert.Single(options.CacheConfigurators);
        Assert.Same(entryOptions, configurator.Configure("orders"));
        Assert.Null(configurator.Configure("others"));
    }

    /// <summary>
    /// 按泛型登记的配置器使用缓存项上的缓存名
    /// </summary>
    [Fact]
    public void ConfigureCache_ByGenericItem_UsesCacheNameAttribute()
    {
        var entryOptions = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanDistributedCacheOptions();

        options.ConfigureCache<SampleCacheItem>(entryOptions);

        var configurator = Assert.Single(options.CacheConfigurators);
        Assert.Same(entryOptions, configurator.Configure("sample"));
    }

    /// <summary>
    /// 按类型登记的配置器与按泛型登记等价
    /// </summary>
    [Fact]
    public void ConfigureCache_ByType_MatchesSameCacheName()
    {
        var entryOptions = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanDistributedCacheOptions();

        options.ConfigureCache(typeof(SampleCacheItem), entryOptions);

        var configurator = Assert.Single(options.CacheConfigurators);
        Assert.Same(entryOptions, configurator.Configure("sample"));
    }

    /// <summary>
    /// 多次登记按登记顺序累积
    /// </summary>
    [Fact]
    public void ConfigureCache_CalledMultipleTimes_AccumulatesConfigurators()
    {
        var options = new XiHanDistributedCacheOptions();

        options.ConfigureCache("a", new DistributedCacheEntryOptions());
        options.ConfigureCache("b", new DistributedCacheEntryOptions());

        Assert.Equal(2, options.CacheConfigurators.Count);
    }

    /// <summary>
    /// 按名称匹配的配置器命中同名时返回登记的选项
    /// </summary>
    [Fact]
    public void NamedConfigurator_OnMatchingName_ReturnsOptions()
    {
        var entryOptions = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(45) };
        var configurator = new NamedDistributedCacheOptionsConfigurator("orders", entryOptions);

        Assert.Same(entryOptions, configurator.Configure("orders"));
    }

    /// <summary>
    /// 按名称匹配的配置器对不同名与大小写不同的名称都不命中
    /// </summary>
    /// <remarks>
    /// 缓存名来自类型全名或显式标注，是区分大小写的标识，这里明确不做忽略大小写的宽松匹配。
    /// </remarks>
    [Theory]
    [InlineData("Orders")]
    [InlineData("orders2")]
    [InlineData("")]
    public void NamedConfigurator_OnDifferentName_ReturnsNull(string targetCacheName)
    {
        var configurator = new NamedDistributedCacheOptionsConfigurator(
            "orders",
            new DistributedCacheEntryOptions());

        Assert.Null(configurator.Configure(targetCacheName));
    }
}
