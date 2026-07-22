// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存事件类型
/// </summary>
public enum CacheEventType
{
    /// <summary>
    /// 缓存项已添加
    /// </summary>
    Added,

    /// <summary>
    /// 缓存项已移除
    /// </summary>
    Removed,

    /// <summary>
    /// 缓存项已过期
    /// </summary>
    Expired,

    /// <summary>
    /// 缓存项已淘汰
    /// </summary>
    Evicted,

    /// <summary>
    /// 缓存项已更新
    /// </summary>
    Updated
}
