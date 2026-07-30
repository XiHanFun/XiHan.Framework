// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存淘汰策略
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>
    /// 最近最少使用（Least Recently Used）
    /// </summary>
    Lru,

    /// <summary>
    /// 先进先出（First In First Out）
    /// </summary>
    Fifo,

    /// <summary>
    /// 最少使用频率（Least Frequently Used）
    /// </summary>
    Lfu
}
