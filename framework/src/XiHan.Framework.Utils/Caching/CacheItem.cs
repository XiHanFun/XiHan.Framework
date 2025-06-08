#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheItem
// Guid:3d5bcfb5-0ab1-4515-9315-564a39a2250e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/9 6:29:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 内存缓存项
/// </summary>
/// <typeparam name="T">缓存项类型</typeparam>
public class CacheItem<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">缓存值</param>
    /// <param name="expiresAt">过期时间</param>
    public CacheItem(T value, DateTime? expiresAt = null)
    {
        Value = value;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// 值
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 是否过期
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.Now >= ExpiresAt.Value;
}
