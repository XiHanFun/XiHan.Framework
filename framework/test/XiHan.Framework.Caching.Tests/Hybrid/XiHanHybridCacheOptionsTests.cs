// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Tests.Fakes;

namespace XiHan.Framework.Caching.Tests.Hybrid;

/// <summary>
/// 曦寒混合缓存选项测试
/// </summary>
/// <remarks>
/// 与分布式缓存选项保持同构：三个 ConfigureCache 重载都收敛到按缓存名匹配的配置器，
/// 缓存名的推导规则与 CacheNameAttribute 一致，两套缓存对同一个缓存项应给出同一个缓存名。
/// </remarks>
public class XiHanHybridCacheOptionsTests
{
    /// <summary>
    /// 新建选项的默认值
    /// </summary>
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var options = new XiHanHybridCacheOptions();

        Assert.True(options.HideErrors);
        Assert.Equal(string.Empty, options.KeyPrefix);
        Assert.Empty(options.CacheConfigurators);
        Assert.NotNull(options.GlobalHybridCacheEntryOptions);
        Assert.Null(options.GlobalHybridCacheEntryOptions.Expiration);
        Assert.Null(options.GlobalHybridCacheEntryOptions.LocalCacheExpiration);
    }

    /// <summary>
    /// 按名称登记的配置器只对同名缓存生效
    /// </summary>
    [Fact]
    public void ConfigureCache_ByName_MatchesOnlyThatCacheName()
    {
        var entryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanHybridCacheOptions();

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
        var entryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanHybridCacheOptions();

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
        var entryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(2) };
        var options = new XiHanHybridCacheOptions();

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
        var options = new XiHanHybridCacheOptions();

        options.ConfigureCache("a", new HybridCacheEntryOptions());
        options.ConfigureCache("b", new HybridCacheEntryOptions());

        Assert.Equal(2, options.CacheConfigurators.Count);
    }

    /// <summary>
    /// 按名称匹配的配置器命中同名时返回登记的选项
    /// </summary>
    [Fact]
    public void NamedConfigurator_OnMatchingName_ReturnsOptions()
    {
        var entryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(45) };
        var configurator = new NamedHybridCacheOptionsConfigurator("orders", entryOptions);

        Assert.Same(entryOptions, configurator.Configure("orders"));
    }

    /// <summary>
    /// 按名称匹配的配置器对不同名与大小写不同的名称都不命中
    /// </summary>
    [Theory]
    [InlineData("Orders")]
    [InlineData("orders2")]
    [InlineData("")]
    public void NamedConfigurator_OnDifferentName_ReturnsNull(string targetCacheName)
    {
        var configurator = new NamedHybridCacheOptionsConfigurator("orders", new HybridCacheEntryOptions());

        Assert.Null(configurator.Configure(targetCacheName));
    }
}
