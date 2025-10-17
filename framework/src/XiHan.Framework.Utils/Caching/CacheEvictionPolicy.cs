#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheEvictionPolicy
// Guid:58c35942-8941-47cc-bc4e-47fc66afc0f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 5:22:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
