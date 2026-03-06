#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHybridCacheOptionsConfigurator
// Guid:b6638491-1f97-4ce9-9281-54e4f66af1df
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 17:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Hybrid;

namespace XiHan.Framework.Caching.Hybrid.Abstracts;

/// <summary>
/// 混合缓存条目选项配置器
/// </summary>
public interface IHybridCacheOptionsConfigurator
{
    /// <summary>
    /// 获取指定缓存名称的缓存条目选项
    /// </summary>
    /// <param name="cacheName">缓存名称</param>
    /// <returns>命中时返回选项；否则返回 null</returns>
    HybridCacheEntryOptions? Configure(string cacheName);
}
