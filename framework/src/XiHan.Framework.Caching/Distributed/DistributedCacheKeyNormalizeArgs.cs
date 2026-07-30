// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 分布式缓存键规范化参数
/// </summary>
public class DistributedCacheKeyNormalizeArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cacheName"></param>
    /// <param name="ignoreMultiTenancy"></param>
    public DistributedCacheKeyNormalizeArgs(string key, string cacheName, bool ignoreMultiTenancy)
    {
        Key = key;
        CacheName = cacheName;
        IgnoreMultiTenancy = ignoreMultiTenancy;
    }

    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 缓存名称
    /// </summary>
    public string CacheName { get; }

    /// <summary>
    /// 是否忽略多租户
    /// </summary>
    public bool IgnoreMultiTenancy { get; }
}
