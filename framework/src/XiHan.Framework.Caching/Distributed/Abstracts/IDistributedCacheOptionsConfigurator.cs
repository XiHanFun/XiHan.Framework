// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 分布式缓存条目选项配置器
/// </summary>
public interface IDistributedCacheOptionsConfigurator
{
    /// <summary>
    /// 获取指定缓存名称的缓存条目选项
    /// </summary>
    /// <param name="cacheName">缓存名称</param>
    /// <returns>命中时返回选项；否则返回 null</returns>
    DistributedCacheEntryOptions? Configure(string cacheName);
}
