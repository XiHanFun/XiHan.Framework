#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICacheSupportsMultipleItems
// Guid:ef8239dc-069b-48c1-a641-c17ebb08f37d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 4:55:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;

namespace XiHan.Framework.Caching;

/// <summary>
/// 缓存支持多项接口
/// </summary>
public interface ICacheSupportsMultipleItems
{
    /// <summary>
    /// 获取多个
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    byte[]?[] GetMany(IEnumerable<string> keys);

    /// <summary>
    /// 获取多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<byte[]?[]> GetManyAsync(IEnumerable<string> keys, CancellationToken token = default);

    /// <summary>
    /// 设置多个
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    void SetMany(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options);

    /// <summary>
    /// 设置多个
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SetManyAsync(IEnumerable<KeyValuePair<string, byte[]>> items, DistributedCacheEntryOptions options, CancellationToken token = default);

    /// <summary>
    /// 刷新多个
    /// </summary>
    /// <param name="keys"></param>
    void RefreshMany(IEnumerable<string> keys);

    /// <summary>
    /// 刷新多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RefreshManyAsync(IEnumerable<string> keys, CancellationToken token = default);

    /// <summary>
    /// 移除多个
    /// </summary>
    /// <param name="keys"></param>
    void RemoveMany(IEnumerable<string> keys);

    /// <summary>
    /// 移除多个
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken token = default);
}
