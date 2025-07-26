#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkCacheAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架缓存特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrameworkCacheAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="cacheMinutes">缓存时间（分钟）</param>
    /// <param name="cacheStrategy">缓存策略</param>
    public FrameworkCacheAttribute(string cacheKey, int cacheMinutes = 60, string cacheStrategy = "Default")
    {
        CacheKey = cacheKey;
        CacheMinutes = cacheMinutes;
        CacheStrategy = cacheStrategy;
    }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string CacheKey { get; }

    /// <summary>
    /// 缓存时间（分钟）
    /// </summary>
    public int CacheMinutes { get; }

    /// <summary>
    /// 缓存策略
    /// </summary>
    public string CacheStrategy { get; }
}
