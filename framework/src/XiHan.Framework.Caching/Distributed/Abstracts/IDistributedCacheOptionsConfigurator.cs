#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedCacheOptionsConfigurator
// Guid:9d1d9d15-8d5f-4026-b026-2e4cf3fa116a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 17:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
