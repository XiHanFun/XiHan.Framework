#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheOptions
// Guid:9a8b7c6d-5e4f-3d2c-1b0a-9f8e7d6c5b4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存配置选项
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// 最大缓存项数量，默认为 10000
    /// 设置为 0 表示不限制
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// 启用缓存统计，默认为 false
    /// 启用后会记录命中率等统计信息，但会有轻微的性能开销
    /// </summary>
    public bool EnableStatistics { get; set; } = false;

    /// <summary>
    /// 启用缓存事件，默认为 false
    /// </summary>
    public bool EnableEvents { get; set; } = false;

    /// <summary>
    /// 惰性清理批次大小，默认为 64
    /// </summary>
    public int CleanupBatchSize { get; set; } = 64;

    /// <summary>
    /// 淘汰策略，默认为 LRU
    /// </summary>
    public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.Lru;
}
