// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 按缓存名称匹配的分布式缓存选项配置器
/// </summary>
/// <param name="cacheName"></param>
/// <param name="options"></param>
public class NamedDistributedCacheOptionsConfigurator(string cacheName, DistributedCacheEntryOptions? options) : IDistributedCacheOptionsConfigurator
{
    /// <summary>
    /// 获取指定缓存名称的缓存条目选项
    /// </summary>
    /// <param name="targetCacheName"></param>
    /// <returns></returns>
    public DistributedCacheEntryOptions? Configure(string targetCacheName)
    {
        return cacheName == targetCacheName ? options : null;
    }
}
