#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICacheItem
// Guid:3a4f1c73-4a9b-4f0e-ae2e-7c3a0d9e4f21
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// 更新访问时间（用于滑动过期）
    /// </summary>
    void UpdateLastAccessed();
}


