// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存项接口，统一过期判断与访问更新时间
/// </summary>
public interface ICacheItem
{
    /// <summary>
    /// 是否过期
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// 下一个过期时间，如果返回 null 表示不过期
    /// </summary>
    DateTimeOffset? NextExpiration { get; }

    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    DateTimeOffset LastAccessed { get; }

    /// <summary>
    /// 访问次数
    /// </summary>
    long AccessCount { get; }

    /// <summary>
    /// 更新访问时间（用于滑动过期）
    /// </summary>
    void UpdateLastAccessed();
}
