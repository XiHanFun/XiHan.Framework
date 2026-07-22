// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;

namespace XiHan.Framework.Caching.Hybrid.Abstracts;

/// <summary>
/// 混合缓存接口
/// </summary>
public interface IHybridCache<TCacheItem> : IHybridCache<TCacheItem, string>
    where TCacheItem : class
{
    /// <summary>
    /// 内部缓存
    /// </summary>
    IHybridCache<TCacheItem, string> InternalCache { get; }
}

/// <summary>
/// 混合缓存接口
/// </summary>
/// <typeparam name="TCacheItem"></typeparam>
/// <typeparam name="TCacheKey"></typeparam>
public interface IHybridCache<TCacheItem, TCacheKey>
    where TCacheItem : class
{
    /// <summary>
    /// 获取或添加缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task<TCacheItem?> GetOrCreateAsync(
        TCacheKey key,
        Func<Task<TCacheItem>> factory,
        Func<HybridCacheEntryOptions>? optionsFactory = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);

    /// <summary>
    /// 设置缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SetAsync(
        TCacheKey key,
        TCacheItem value,
        HybridCacheEntryOptions? options = null,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);

    /// <summary>
    /// 移除缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RemoveAsync(
        TCacheKey key,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);

    /// <summary>
    /// 移除多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task RemoveManyAsync(
        IEnumerable<TCacheKey> keys,
        bool? hideErrors = null,
        bool considerUow = false,
        CancellationToken token = default);
}
