#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedCacheKeyNormalizeArgs
// Guid:52411820-11d3-47fd-8b79-85c0131952a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:43:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching;

/// <summary>
/// 分布式缓存键规范化参数
/// </summary>
public class DistributedCacheKeyNormalizeArgs
{
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
}
