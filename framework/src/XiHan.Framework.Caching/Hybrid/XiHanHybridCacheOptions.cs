// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Hybrid.Abstracts;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存选项
/// </summary>
public class XiHanHybridCacheOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanHybridCacheOptions()
    {
        CacheConfigurators = [];
        GlobalHybridCacheEntryOptions = new HybridCacheEntryOptions();
        KeyPrefix = "";
    }

    /// <summary>
    /// 是否隐藏错误
    /// </summary>
    public bool HideErrors { get; set; } = true;

    /// <summary>
    /// 缓存 Key 前缀
    /// </summary>
    /// <remarks>
    /// 拼在规范化键「租户段:缓存名段:业务键」之前，用于同一缓存实例上多应用互相隔离，例如 <c>"app1:"</c>。
    /// 分隔符不会自动补，需要什么形状就照原样写进来。默认空串表示不加前缀。
    /// 一二级缓存共用这一个出键口径，改动后已写入的旧键将读不回来。
    /// </remarks>
    public string KeyPrefix { get; set; }

    /// <summary>
    /// 全局缓存条目选项
    /// </summary>
    public HybridCacheEntryOptions GlobalHybridCacheEntryOptions { get; set; }

    /// <summary>
    /// 缓存配置器
    /// </summary>
    public List<IHybridCacheOptionsConfigurator> CacheConfigurators { get; set; }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <typeparam name="TCacheItem"></typeparam>
    /// <param name="options"></param>
    public void ConfigureCache<TCacheItem>(HybridCacheEntryOptions? options)
    {
        ConfigureCache(typeof(TCacheItem), options);
    }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <param name="cacheItemType"></param>
    /// <param name="options"></param>
    public void ConfigureCache(Type cacheItemType, HybridCacheEntryOptions? options)
    {
        ConfigureCache(CacheNameAttribute.GetCacheName(cacheItemType), options);
    }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <param name="cacheName"></param>
    /// <param name="options"></param>
    public void ConfigureCache(string cacheName, HybridCacheEntryOptions? options)
    {
        CacheConfigurators.Add(new NamedHybridCacheOptionsConfigurator(cacheName, options));
    }
}
