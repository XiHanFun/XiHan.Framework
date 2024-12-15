#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDistributedCacheOptions
// Guid:706bc129-003c-4772-92e1-219c0e702106
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:45:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;

namespace XiHan.Framework.Caching;

/// <summary>
/// 曦寒分布式缓存选项
/// </summary>
public class XiHanDistributedCacheOptions
{
    /// <summary>
    /// 是否隐藏错误
    /// </summary>
    public bool HideErrors { get; set; } = true;

    /// <summary>
    /// 缓存键前缀
    /// </summary>
    public string KeyPrefix { get; set; }

    /// <summary>
    /// 全局缓存条目选项
    /// </summary>
    public DistributedCacheEntryOptions GlobalCacheEntryOptions { get; set; }

    /// <summary>
    /// 缓存配置器
    /// </summary>
    public List<Func<string, DistributedCacheEntryOptions?>> CacheConfigurators { get; set; } //TODO: 是否使用配置器接口来代替？

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanDistributedCacheOptions()
    {
        CacheConfigurators = [];
        GlobalCacheEntryOptions = new DistributedCacheEntryOptions();
        KeyPrefix = "";
    }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <typeparam name="TCacheItem"></typeparam>
    /// <param name="options"></param>
    public void ConfigureCache<TCacheItem>(DistributedCacheEntryOptions? options)
    {
        ConfigureCache(typeof(TCacheItem), options);
    }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <param name="cacheItemType"></param>
    /// <param name="options"></param>
    public void ConfigureCache(Type cacheItemType, DistributedCacheEntryOptions? options)
    {
        ConfigureCache(CacheNameAttribute.GetCacheName(cacheItemType), options);
    }

    /// <summary>
    /// 配置缓存
    /// </summary>
    /// <param name="cacheName"></param>
    /// <param name="options"></param>
    public void ConfigureCache(string cacheName, DistributedCacheEntryOptions? options)
    {
        CacheConfigurators.Add(name => cacheName != name ? null : options);
    }
}
