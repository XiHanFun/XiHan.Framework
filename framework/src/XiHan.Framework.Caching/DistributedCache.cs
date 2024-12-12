﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedCache
// Guid:cc2e17e9-ff49-46f9-8d63-ebcebfcf939f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 5:39:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Caching;

/// <summary>
/// <typeparamref name="TCacheItem" /> 类型的分布式缓存
/// </summary>
/// <typeparam name="TCacheItem">缓存项的类型</typeparam>
public class DistributedCache<TCacheItem> : IDistributedCache<TCacheItem> where TCacheItem : class
{
    /// <summary>
    /// 获取内部缓存
    /// </summary>
    public IDistributedCache<TCacheItem, string> InternalCache { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="internalCache"></param>
    public DistributedCache(IDistributedCache<TCacheItem, string> internalCache)
    {
        InternalCache = internalCache;
    }

    /// <summary>
    /// 获取缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public TCacheItem? Get(string key, bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.Get(key, hideErrors, considerUow);
    }

    /// <summary>
    /// 获取多个键对应的缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public KeyValuePair<string, TCacheItem?>[] GetMany(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.GetMany(keys, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步获取多个键对应的缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<KeyValuePair<string, TCacheItem?>[]> GetManyAsync(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.GetManyAsync(keys, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 获取缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<TCacheItem?> GetAsync([NotNull] string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.GetAsync(key, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 获取缓存项或添加缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public TCacheItem? GetOrAdd(string key, Func<TCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.GetOrAdd(key, factory, optionsFactory, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步获取缓存项或添加缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<TCacheItem?> GetOrAddAsync([NotNull] string key, Func<Task<TCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.GetOrAddAsync(key, factory, optionsFactory, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 获取多个键对应的缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public KeyValuePair<string, TCacheItem?>[] GetOrAddMany(IEnumerable<string> keys, Func<IEnumerable<string>, List<KeyValuePair<string, TCacheItem>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.GetOrAddMany(keys, factory, optionsFactory, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步获取多个键对应的缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<KeyValuePair<string, TCacheItem?>[]> GetOrAddManyAsync(IEnumerable<string> keys, Func<IEnumerable<string>, Task<List<KeyValuePair<string, TCacheItem>>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.GetOrAddManyAsync(keys, factory, optionsFactory, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 设置缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void Set(string key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
    {
        InternalCache.Set(key, value, options, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步设置缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task SetAsync([NotNull] string key, [NotNull] TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.SetAsync(key, value, options, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 设置多个缓存项
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void SetMany(IEnumerable<KeyValuePair<string, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
    {
        InternalCache.SetMany(items, options, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步设置多个缓存项
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task SetManyAsync(IEnumerable<KeyValuePair<string, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.SetManyAsync(items, options, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 刷新缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    public void Refresh(string key, bool? hideErrors = null)
    {
        InternalCache.Refresh(key, hideErrors);
    }

    /// <summary>
    /// 异步刷新缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task RefreshAsync(string key, bool? hideErrors = null, CancellationToken token = default)
    {
        return InternalCache.RefreshAsync(key, hideErrors, token);
    }

    /// <summary>
    /// 刷新多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    public void RefreshMany(IEnumerable<string> keys, bool? hideErrors = null)
    {
        InternalCache.RefreshMany(keys, hideErrors);
    }

    /// <summary>
    /// 异步刷新多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task RefreshManyAsync(IEnumerable<string> keys, bool? hideErrors = null, CancellationToken token = default)
    {
        return InternalCache.RefreshManyAsync(keys, hideErrors, token);
    }

    /// <summary>
    /// 移除缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void Remove(string key, bool? hideErrors = null, bool considerUow = false)
    {
        InternalCache.Remove(key, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步移除缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task RemoveAsync(string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.RemoveAsync(key, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 移除多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void RemoveMany(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false)
    {
        InternalCache.RemoveMany(keys, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步移除多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task RemoveManyAsync(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.RemoveManyAsync(keys, hideErrors, considerUow, token);
    }
}

///// <summary>
///// Represents a distributed cache of <typeparamref name="TCacheItem" /> type.
///// Uses a generic cache key type of <typeparamref name="TCacheKey" /> type.
///// </summary>
///// <typeparam name="TCacheItem">The type of cache item being cached.</typeparam>
///// <typeparam name="TCacheKey">The type of cache key being used.</typeparam>
//public class DistributedCache<TCacheItem, TCacheKey> : IDistributedCache<TCacheItem, TCacheKey> where TCacheItem : class where TCacheKey : notnull
//{
//    /// <summary>
//    ///
//    /// </summary>
//    public const string UowCacheName = "XiHanDistributedCache";

//    /// <summary>
//    ///
//    /// </summary>
//    public ILogger<DistributedCache<TCacheItem, TCacheKey>> Logger { get; set; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected string CacheName { get; set; } = default!;

//    /// <summary>
//    ///
//    /// </summary>
//    protected bool IgnoreMultiTenancy { get; set; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected IDistributedCache Cache { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected ICancellationTokenProvider CancellationTokenProvider { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected IDistributedCacheSerializer Serializer { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected IDistributedCacheKeyNormalizer KeyNormalizer { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected IServiceScopeFactory ServiceScopeFactory { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected IUnitOfWorkManager UnitOfWorkManager { get; }

//    /// <inheritdoc/>
//    protected SemaphoreSlim SyncSemaphore { get; }

//    /// <summary>
//    ///
//    /// </summary>
//    protected DistributedCacheEntryOptions DefaultCacheOptions = default!;

//    private readonly XiHanDistributedCacheOptions _distributedCacheOption;

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="distributedCacheOption"></param>
//    /// <param name="cache"></param>
//    /// <param name="cancellationTokenProvider"></param>
//    /// <param name="serializer"></param>
//    /// <param name="keyNormalizer"></param>
//    /// <param name="serviceScopeFactory"></param>
//    /// <param name="unitOfWorkManager"></param>
//    public DistributedCache(IOptions<XiHanDistributedCacheOptions> distributedCacheOption, IDistributedCache cache, ICancellationTokenProvider cancellationTokenProvider, IDistributedCacheSerializer serializer, IDistributedCacheKeyNormalizer keyNormalizer, IServiceScopeFactory serviceScopeFactory, IUnitOfWorkManager unitOfWorkManager)
//    {
//        _distributedCacheOption = distributedCacheOption.Value;
//        Cache = cache;
//        CancellationTokenProvider = cancellationTokenProvider;
//        Logger = NullLogger<DistributedCache<TCacheItem, TCacheKey>>.Instance;
//        Serializer = serializer;
//        KeyNormalizer = keyNormalizer;
//        ServiceScopeFactory = serviceScopeFactory;
//        UnitOfWorkManager = unitOfWorkManager;
//        SyncSemaphore = new SemaphoreSlim(1, 1);
//        SetDefaultOptions();
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <returns></returns>
//    protected virtual string NormalizeKey(TCacheKey key)
//    {
//        return KeyNormalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs(key.ToString()!, CacheName, IgnoreMultiTenancy));
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <returns></returns>
//    protected virtual DistributedCacheEntryOptions GetDefaultCacheEntryOptions()
//    {
//        foreach (var configure in _distributedCacheOption.CacheConfigurators)
//        {
//            var options = configure.Invoke(CacheName);
//            if (options != null)
//            {
//                return options;
//            }
//        }

//        return _distributedCacheOption.GlobalCacheEntryOptions;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    protected virtual void SetDefaultOptions()
//    {
//        CacheName = CacheNameAttribute.GetCacheName<TCacheItem>();

//        //IgnoreMultiTenancy
//        IgnoreMultiTenancy = typeof(TCacheItem).IsDefined(typeof(IgnoreMultiTenancyAttribute), true);

//        //Configure default cache entry options
//        DefaultCacheOptions = GetDefaultCacheEntryOptions();
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    public virtual TCacheItem? Get(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        if (ShouldConsiderUow(considerUow))
//        {
//            var value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
//            if (value != null)
//            {
//                return value;
//            }
//        }

//        byte[]? cachedBytes;
//        try
//        {
//            cachedBytes = Cache.Get(NormalizeKey(key));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return null;
//            }

//            throw;
//        }

//        return ToCacheItem(cachedBytes);
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    public virtual KeyValuePair<TCacheKey, TCacheItem?>[] GetMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false)
//    {
//        var keyArray = keys.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            return GetManyFallback(keyArray, hideErrors, considerUow);
//        }

//        var notCachedKeys = new List<TCacheKey>();
//        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            foreach (var key in keyArray)
//            {
//                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
//                if (value != null)
//                {
//                    cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
//                }
//            }

//            notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
//            if (notCachedKeys.Count == 0)
//            {
//                return [.. cachedValues];
//            }
//        }

//        hideErrors ??= _distributedCacheOption.HideErrors;
//        byte[]?[] cachedBytes;
//        var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
//        try
//        {
//            cachedBytes = cacheSupportsMultipleItems.GetMany(readKeys.Select(NormalizeKey));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return ToCacheItemsWithDefaultValues(keyArray);
//            }

//            throw;
//        }

//        return [.. cachedValues, .. ToCacheItems(cachedBytes, readKeys)];
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    protected virtual KeyValuePair<TCacheKey, TCacheItem?>[] GetManyFallback(TCacheKey[] keys, bool? hideErrors = null, bool considerUow = false)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            return keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem?>(key, Get(key, false, considerUow))).ToArray();
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return ToCacheItemsWithDefaultValues(keys);
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        var keyArray = keys.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            return await GetManyFallbackAsync(keyArray, hideErrors, considerUow, token);
//        }

//        var notCachedKeys = new List<TCacheKey>();
//        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            foreach (var key in keyArray)
//            {
//                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
//                if (value != null)
//                {
//                    cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
//                }
//            }

//            notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
//            if (notCachedKeys.Count == 0)
//            {
//                return [.. cachedValues];
//            }
//        }

//        hideErrors ??= _distributedCacheOption.HideErrors;
//        byte[]?[] cachedBytes;
//        var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
//        try
//        {
//            cachedBytes = await cacheSupportsMultipleItems.GetManyAsync(readKeys.Select(NormalizeKey), CancellationTokenProvider.FallbackToProvider(token));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return ToCacheItemsWithDefaultValues(keyArray);
//            }

//            throw;
//        }

//        return [.. cachedValues, .. ToCacheItems(cachedBytes, readKeys)];
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    protected virtual async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetManyFallbackAsync(TCacheKey[] keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            var result = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//            foreach (var key in keys)
//            {
//                result.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, await GetAsync(key, false, considerUow, token)));
//            }

//            return [.. result];
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return ToCacheItemsWithDefaultValues(keys);
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task<TCacheItem?> GetAsync([NotNull] TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        if (ShouldConsiderUow(considerUow))
//        {
//            var value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
//            if (value != null)
//            {
//                return value;
//            }
//        }

//        byte[]? cachedBytes;
//        try
//        {
//            cachedBytes = await Cache.GetAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return null;
//            }

//            throw;
//        }

//        return cachedBytes == null ? null : Serializer.Deserialize<TCacheItem>(cachedBytes);
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="factory"></param>
//    /// <param name="optionsFactory"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    public virtual TCacheItem? GetOrAdd(TCacheKey key, Func<TCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
//    {
//        var value = Get(key, hideErrors, considerUow);
//        if (value != null)
//        {
//            return value;
//        }

//        using (SyncSemaphore.Lock())
//        {
//            value = Get(key, hideErrors, considerUow);
//            if (value != null)
//            {
//                return value;
//            }

//            value = factory();
//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                if (uowCache.TryGetValue(key, out var item))
//                {
//                    _ = item.SetValue(value);
//                }
//                else
//                {
//                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
//                }
//            }

//            Set(key, value, optionsFactory?.Invoke(), hideErrors, considerUow);
//        }

//        return value;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="factory"></param>
//    /// <param name="optionsFactory"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task<TCacheItem?> GetOrAddAsync([NotNull] TCacheKey key, Func<Task<TCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        token = CancellationTokenProvider.FallbackToProvider(token);
//        var value = await GetAsync(key, hideErrors, considerUow, token);
//        if (value != null)
//        {
//            return value;
//        }

//        using (await SyncSemaphore.LockAsync(token))
//        {
//            value = await GetAsync(key, hideErrors, considerUow, token);
//            if (value != null)
//            {
//                return value;
//            }

//            value = await factory();
//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                if (uowCache.TryGetValue(key, out var item))
//                {
//                    _ = item.SetValue(value);
//                }
//                else
//                {
//                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
//                }
//            }

//            await SetAsync(key, value, optionsFactory?.Invoke(), hideErrors, considerUow, token);
//        }

//        return value;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="factory"></param>
//    /// <param name="optionsFactory"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    public KeyValuePair<TCacheKey, TCacheItem?>[] GetOrAddMany(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, List<KeyValuePair<TCacheKey, TCacheItem>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
//    {
//        KeyValuePair<TCacheKey, TCacheItem?>[] result;
//        var keyArray = keys.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            result = GetManyFallback(keyArray, hideErrors, considerUow);
//        }
//        else
//        {
//            var notCachedKeys = new List<TCacheKey>();
//            var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                foreach (var key in keyArray)
//                {
//                    var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
//                    if (value != null)
//                    {
//                        cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
//                    }
//                }

//                notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
//                if (notCachedKeys.Count == 0)
//                {
//                    return [.. cachedValues];
//                }
//            }

//            hideErrors ??= _distributedCacheOption.HideErrors;
//            byte[]?[] cachedBytes;
//            var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
//            try
//            {
//                cachedBytes = cacheSupportsMultipleItems.GetMany(readKeys.Select(NormalizeKey));
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    HandleException(ex);
//                    return ToCacheItemsWithDefaultValues(keyArray);
//                }

//                throw;
//            }

//            result = [.. cachedValues, .. ToCacheItems(cachedBytes, readKeys)];
//        }

//        if (result.All(x => x.Value != null))
//        {
//            return result!;
//        }

//        var missingKeys = new List<TCacheKey>();
//        var missingValuesIndex = new List<int>();
//        for (var i = 0; i < keyArray.Length; i++)
//        {
//            if (result[i].Value != null)
//            {
//                continue;
//            }

//            missingKeys.Add(keyArray[i]);
//            missingValuesIndex.Add(i);
//        }

//        var missingValues = factory.Invoke(missingKeys).ToArray();
//        var valueQueue = new Queue<KeyValuePair<TCacheKey, TCacheItem>>(missingValues);
//        SetMany(missingValues, optionsFactory?.Invoke(), hideErrors, considerUow);
//        foreach (var index in missingValuesIndex)
//        {
//            result[index] = valueQueue.Dequeue()!;
//        }

//        return result;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="factory"></param>
//    /// <param name="optionsFactory"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetOrAddManyAsync(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, Task<List<KeyValuePair<TCacheKey, TCacheItem>>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        KeyValuePair<TCacheKey, TCacheItem?>[] result;
//        var keyArray = keys.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            result = await GetManyFallbackAsync(keyArray, hideErrors, considerUow, token);
//        }
//        else
//        {
//            var notCachedKeys = new List<TCacheKey>();
//            var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                foreach (var key in keyArray)
//                {
//                    var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
//                    if (value != null)
//                    {
//                        cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
//                    }
//                }

//                notCachedKeys = keyArray.Except(cachedValues.Select(x => x.Key)).ToList();
//                if (notCachedKeys.Count == 0)
//                {
//                    return [.. cachedValues];
//                }
//            }

//            hideErrors ??= _distributedCacheOption.HideErrors;
//            byte[]?[] cachedBytes;
//            var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
//            try
//            {
//                cachedBytes = await cacheSupportsMultipleItems.GetManyAsync(readKeys.Select(NormalizeKey), token);
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    await HandleExceptionAsync(ex);
//                    return ToCacheItemsWithDefaultValues(keyArray);
//                }

//                throw;
//            }

//            result = [.. cachedValues, .. ToCacheItems(cachedBytes, readKeys)];
//        }

//        if (result.All(x => x.Value != null))
//        {
//            return result;
//        }

//        var missingKeys = new List<TCacheKey>();
//        var missingValuesIndex = new List<int>();
//        for (var i = 0; i < keyArray.Length; i++)
//        {
//            if (result[i].Value != null)
//            {
//                continue;
//            }

//            missingKeys.Add(keyArray[i]);
//            missingValuesIndex.Add(i);
//        }

//        var missingValues = (await factory.Invoke(missingKeys)).ToArray();
//        var valueQueue = new Queue<KeyValuePair<TCacheKey, TCacheItem>>(missingValues);
//        await SetManyAsync(missingValues, optionsFactory?.Invoke(), hideErrors, considerUow, token);
//        foreach (var index in missingValuesIndex)
//        {
//            result[index] = valueQueue.Dequeue()!;
//        }

//        return result;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="value"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    public virtual void Set(TCacheKey key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
//    {
//        void SetRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                Cache.Set(NormalizeKey(key), Serializer.Serialize(value), options ?? DefaultCacheOptions);
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    HandleException(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            if (uowCache.TryGetValue(key, out _))
//            {
//                _ = uowCache[key].SetValue(value);
//            }
//            else
//            {
//                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
//            }

//            UnitOfWorkManager.Current?.OnCompleted(() =>
//            {
//                SetRealCache();
//                return Task.CompletedTask;
//            });
//        }
//        else
//        {
//            SetRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="value"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task SetAsync([NotNull] TCacheKey key, [NotNull] TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        async Task SetRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                await Cache.SetAsync(NormalizeKey(key), Serializer.Serialize(value), options ?? DefaultCacheOptions, CancellationTokenProvider.FallbackToProvider(token));
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    await HandleExceptionAsync(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            if (uowCache.TryGetValue(key, out _))
//            {
//                _ = uowCache[key].SetValue(value);
//            }
//            else
//            {
//                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
//            }

//            UnitOfWorkManager.Current?.OnCompleted(SetRealCache);
//        }
//        else
//        {
//            await SetRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="items"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    public void SetMany(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
//    {
//        var itemsArray = items.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            SetManyFallback(itemsArray, options, hideErrors, considerUow);
//            return;
//        }

//        void SetRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                cacheSupportsMultipleItems.SetMany(ToRawCacheItems(itemsArray), options ?? DefaultCacheOptions);
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    HandleException(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            foreach (var pair in itemsArray)
//            {
//                if (uowCache.TryGetValue(pair.Key, out _))
//                {
//                    _ = uowCache[pair.Key].SetValue(pair.Value);
//                }
//                else
//                {
//                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
//                }
//            }

//            UnitOfWorkManager.Current?.OnCompleted(() =>
//            {
//                SetRealCache();
//                return Task.CompletedTask;
//            });
//        }
//        else
//        {
//            SetRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="items"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    protected virtual void SetManyFallback(KeyValuePair<TCacheKey, TCacheItem>[] items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            foreach (var item in items)
//            {
//                Set(item.Key, item.Value, options, false, considerUow);
//            }
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="items"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task SetManyAsync(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        var itemsArray = items.ToArray();
//        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            await SetManyFallbackAsync(itemsArray, options, hideErrors, considerUow, token);
//            return;
//        }

//        async Task SetRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                await cacheSupportsMultipleItems.SetManyAsync(ToRawCacheItems(itemsArray), options ?? DefaultCacheOptions, CancellationTokenProvider.FallbackToProvider(token));
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    await HandleExceptionAsync(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            foreach (var pair in itemsArray)
//            {
//                if (uowCache.TryGetValue(pair.Key, out _))
//                {
//                    _ = uowCache[pair.Key].SetValue(pair.Value);
//                }
//                else
//                {
//                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
//                }
//            }

//            UnitOfWorkManager.Current?.OnCompleted(SetRealCache);
//        }
//        else
//        {
//            await SetRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="items"></param>
//    /// <param name="options"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    protected virtual async Task SetManyFallbackAsync(KeyValuePair<TCacheKey, TCacheItem>[] items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            foreach (var item in items)
//            {
//                await SetAsync(item.Key, item.Value, options, false, considerUow, token);
//            }
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    public virtual void Refresh(TCacheKey key, bool? hideErrors = null)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            Cache.Refresh(NormalizeKey(key));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task RefreshAsync(TCacheKey key, bool? hideErrors = null, CancellationToken token = default)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            await Cache.RefreshAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    public virtual void RefreshMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//            {
//                cacheSupportsMultipleItems.RefreshMany(keys.Select(NormalizeKey));
//            }
//            else
//            {
//                foreach (var key in keys)
//                {
//                    Cache.Refresh(NormalizeKey(key));
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                HandleException(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task RefreshManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, CancellationToken token = default)
//    {
//        hideErrors ??= _distributedCacheOption.HideErrors;
//        try
//        {
//            if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//            {
//                await cacheSupportsMultipleItems.RefreshManyAsync(keys.Select(NormalizeKey), token);
//            }
//            else
//            {
//                foreach (var key in keys)
//                {
//                    await Cache.RefreshAsync(NormalizeKey(key), token);
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            if (hideErrors == true)
//            {
//                await HandleExceptionAsync(ex);
//                return;
//            }

//            throw;
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    public virtual void Remove(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
//    {
//        void RemoveRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                Cache.Remove(NormalizeKey(key));
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    HandleException(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            if (uowCache.TryGetValue(key, out _))
//            {
//                _ = uowCache[key].RemoveValue();
//            }

//            UnitOfWorkManager.Current?.OnCompleted(() =>
//            {
//                RemoveRealCache();
//                return Task.CompletedTask;
//            });
//        }
//        else
//        {
//            RemoveRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="key"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public virtual async Task RemoveAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        async Task RemoveRealCache()
//        {
//            hideErrors ??= _distributedCacheOption.HideErrors;
//            try
//            {
//                await Cache.RemoveAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
//            }
//            catch (Exception ex)
//            {
//                if (hideErrors == true)
//                {
//                    await HandleExceptionAsync(ex);
//                    return;
//                }

//                throw;
//            }
//        }

//        if (ShouldConsiderUow(considerUow))
//        {
//            var uowCache = GetUnitOfWorkCache();
//            if (uowCache.TryGetValue(key, out _))
//            {
//                _ = uowCache[key].RemoveValue();
//            }

//            UnitOfWorkManager.Current?.OnCompleted(RemoveRealCache);
//        }
//        else
//        {
//            await RemoveRealCache();
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    public void RemoveMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false)
//    {
//        var keyArray = keys.ToArray();
//        if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            void RemoveRealCache()
//            {
//                hideErrors ??= _distributedCacheOption.HideErrors;
//                try
//                {
//                    cacheSupportsMultipleItems.RemoveMany(keyArray.Select(NormalizeKey));
//                }
//                catch (Exception ex)
//                {
//                    if (hideErrors == true)
//                    {
//                        HandleException(ex);
//                        return;
//                    }

//                    throw;
//                }
//            }

//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                foreach (var key in keyArray)
//                {
//                    if (uowCache.TryGetValue(key, out _))
//                    {
//                        _ = uowCache[key].RemoveValue();
//                    }
//                }

//                UnitOfWorkManager.Current?.OnCompleted(() =>
//                {
//                    RemoveRealCache();
//                    return Task.CompletedTask;
//                });
//            }
//            else
//            {
//                RemoveRealCache();
//            }
//        }
//        else
//        {
//            foreach (var key in keyArray)
//            {
//                Remove(key, hideErrors, considerUow);
//            }
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <param name="hideErrors"></param>
//    /// <param name="considerUow"></param>
//    /// <param name="token"></param>
//    /// <returns></returns>
//    public async Task RemoveManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
//    {
//        var keyArray = keys.ToArray();
//        if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
//        {
//            async Task RemoveRealCache()
//            {
//                hideErrors ??= _distributedCacheOption.HideErrors;
//                try
//                {
//                    await cacheSupportsMultipleItems.RemoveManyAsync(keyArray.Select(NormalizeKey), token);
//                }
//                catch (Exception ex)
//                {
//                    if (hideErrors == true)
//                    {
//                        await HandleExceptionAsync(ex);
//                        return;
//                    }

//                    throw;
//                }
//            }

//            if (ShouldConsiderUow(considerUow))
//            {
//                var uowCache = GetUnitOfWorkCache();
//                foreach (var key in keyArray)
//                {
//                    if (uowCache.TryGetValue(key, out _))
//                    {
//                        _ = uowCache[key].RemoveValue();
//                    }
//                }

//                UnitOfWorkManager.Current?.OnCompleted(RemoveRealCache);
//            }
//            else
//            {
//                await RemoveRealCache();
//            }
//        }
//        else
//        {
//            foreach (var key in keyArray)
//            {
//                await RemoveAsync(key, hideErrors, considerUow, token);
//            }
//        }
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="ex"></param>
//    protected virtual void HandleException(Exception ex)
//    {
//        _ = HandleExceptionAsync(ex);
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="ex"></param>
//    /// <returns></returns>
//    protected virtual async Task HandleExceptionAsync(Exception ex)
//    {
//        Logger.LogException(ex, LogLevel.Warning);
//        using var scope = ServiceScopeFactory.CreateScope();
//        await scope.ServiceProvider.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(ex, LogLevel.Warning));
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="itemBytes"></param>
//    /// <param name="itemKeys"></param>
//    /// <returns></returns>
//    /// <exception cref="XiHanException"></exception>
//    protected virtual KeyValuePair<TCacheKey, TCacheItem?>[] ToCacheItems(byte[]?[] itemBytes, TCacheKey[] itemKeys)
//    {
//        if (itemBytes.Length != itemKeys.Length)
//        {
//            throw new XiHanException("count of the item bytes should be same with the count of the given keys");
//        }

//        var result = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
//        for (var i = 0; i < itemKeys.Length; i++)
//        {
//            result.Add(new KeyValuePair<TCacheKey, TCacheItem?>(itemKeys[i], ToCacheItem(itemBytes[i])));
//        }

//        return [.. result];
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="bytes"></param>
//    /// <returns></returns>
//    protected virtual TCacheItem? ToCacheItem(byte[]? bytes)
//    {
//        return bytes == null ? null : Serializer.Deserialize<TCacheItem>(bytes);
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="items"></param>
//    /// <returns></returns>
//    protected virtual KeyValuePair<string, byte[]>[] ToRawCacheItems(KeyValuePair<TCacheKey, TCacheItem>[] items)
//    {
//        return items.Select(i => new KeyValuePair<string, byte[]>(NormalizeKey(i.Key), Serializer.Serialize(i.Value))).ToArray();
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="keys"></param>
//    /// <returns></returns>
//    private static KeyValuePair<TCacheKey, TCacheItem?>[] ToCacheItemsWithDefaultValues(TCacheKey[] keys)
//    {
//        return keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem?>(key, default)).ToArray();
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <param name="considerUow"></param>
//    /// <returns></returns>
//    protected virtual bool ShouldConsiderUow(bool considerUow)
//    {
//        return considerUow && UnitOfWorkManager.Current != null;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <returns></returns>
//    protected virtual string GetUnitOfWorkCacheKey()
//    {
//        return UowCacheName + CacheName;
//    }

//    /// <summary>
//    ///
//    /// </summary>
//    /// <returns></returns>
//    /// <exception cref="XiHanException"></exception>
//    protected virtual Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>> GetUnitOfWorkCache()
//    {
//        return UnitOfWorkManager.Current == null ? throw new XiHanException($"There is no active UOW.") : UnitOfWorkManager.Current.GetOrAddItem(GetUnitOfWorkCacheKey(), key => []);
//    }
//}
