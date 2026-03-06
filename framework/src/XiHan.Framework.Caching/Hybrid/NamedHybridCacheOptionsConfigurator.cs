#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NamedHybridCacheOptionsConfigurator
// Guid:07ab34c5-f140-4067-a8b1-f4af8d546ac5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 17:43:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Hybrid;
using XiHan.Framework.Caching.Hybrid.Abstracts;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 按缓存名称匹配的混合缓存选项配置器
/// </summary>
/// <param name="cacheName"></param>
/// <param name="options"></param>
public class NamedHybridCacheOptionsConfigurator(string cacheName, HybridCacheEntryOptions? options) : IHybridCacheOptionsConfigurator
{
    /// <summary>
    /// 获取指定缓存名称的缓存条目选项
    /// </summary>
    /// <param name="targetCacheName"></param>
    /// <returns></returns>
    public HybridCacheEntryOptions? Configure(string targetCacheName)
    {
        return cacheName == targetCacheName ? options : null;
    }
}
