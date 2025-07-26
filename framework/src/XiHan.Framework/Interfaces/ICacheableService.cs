#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICacheableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可缓存服务接口
/// </summary>
public interface ICacheableService : IFrameworkService
{
    /// <summary>
    /// 缓存键前缀
    /// </summary>
    string CacheKeyPrefix { get; }

    /// <summary>
    /// 默认缓存时间（分钟）
    /// </summary>
    int DefaultCacheMinutes { get; }

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <param name="pattern">缓存键模式</param>
    Task ClearCacheAsync(string? pattern = null);

    /// <summary>
    /// 获取缓存统计
    /// </summary>
    /// <returns>缓存统计</returns>
    Task<object> GetCacheStatisticsAsync();
}
