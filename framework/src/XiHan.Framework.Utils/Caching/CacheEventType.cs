#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheEventType
// Guid:a2bbc589-7eba-4035-ad85-b14ef96169de
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 05:23:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
