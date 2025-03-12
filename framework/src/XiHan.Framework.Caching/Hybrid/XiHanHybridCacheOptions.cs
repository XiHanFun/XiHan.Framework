#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHybridCacheOptions
// Guid:aaf54764-7166-4f64-840c-e63284a5fad8
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/3/12 19:58:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Hybrid;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存选项
/// </summary>
public class XiHanHybridCacheOptions
{
    /// <summary>
    /// 是否隐藏错误
    /// </summary>
    public bool HideErrors { get; set; } = true;

    /// <summary>
    /// 缓存 Key 前缀
    /// </summary>
    public string KeyPrefix { get; set; }

    /// <summary>
    /// 全局缓存条目选项
    /// </summary>
    public HybridCacheEntryOptions GlobalHybridCacheEntryOptions { get; set; }

    /// <summary>
    /// 缓存配置器
    /// </summary>
    public List<Func<string, HybridCacheEntryOptions?>> CacheConfigurators { get; set; } //TODO: use a configurator interface instead?

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanHybridCacheOptions()
    {
        CacheConfigurators = new List<Func<string, HybridCacheEntryOptions?>>();
        GlobalHybridCacheEntryOptions = new HybridCacheEntryOptions();
        KeyPrefix = "";
    }

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
        CacheConfigurators.Add(name => cacheName != name ? null : options);
    }
}
