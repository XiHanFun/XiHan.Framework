#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICacheSupportsKeyPattern
// Guid:29ec74b6-e9db-4e3d-96db-d28ba3401886
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/07 16:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 缓存键模式查询支持接口（基于规范化键）
/// </summary>
public interface ICacheSupportsKeyPattern
{
    /// <summary>
    /// 按模式获取键集合
    /// </summary>
    /// <param name="pattern">规范化键模式</param>
    /// <returns></returns>
    string[] GetKeys(string pattern);

    /// <summary>
    /// 异步按模式获取键集合
    /// </summary>
    /// <param name="pattern">规范化键模式</param>
    /// <param name="token">取消令牌</param>
    /// <returns></returns>
    Task<string[]> GetKeysAsync(string pattern, CancellationToken token = default);

    /// <summary>
    /// 按模式移除键
    /// </summary>
    /// <param name="pattern">规范化键模式</param>
    /// <returns>移除数量</returns>
    long RemoveByPattern(string pattern);

    /// <summary>
    /// 异步按模式移除键
    /// </summary>
    /// <param name="pattern">规范化键模式</param>
    /// <param name="token">取消令牌</param>
    /// <returns>移除数量</returns>
    Task<long> RemoveByPatternAsync(string pattern, CancellationToken token = default);
}
