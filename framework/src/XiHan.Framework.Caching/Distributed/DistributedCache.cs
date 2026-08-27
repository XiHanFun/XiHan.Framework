// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.RegularExpressions;
using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Exceptions.Handling;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Extensions.Threading;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Threading;
using XiHan.Framework.Threading.Extensions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Extensions;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 分布式缓存
/// </summary>
/// <typeparam name="TCacheItem">缓存项的类型</typeparam>
public class DistributedCache<TCacheItem> : IDistributedCache<TCacheItem>
    where TCacheItem : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="internalCache"></param>
    public DistributedCache(IDistributedCache<TCacheItem, string> internalCache)
    {
        InternalCache = internalCache;
    }

    /// <summary>
    /// 获取内部缓存
    /// </summary>
    public IDistributedCache<TCacheItem, string> InternalCache { get; }

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
    public Task<TCacheItem?> GetAsync(string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
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
    public Task<TCacheItem?> GetOrAddAsync(string key, Func<Task<TCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
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
    public Task SetAsync(string key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
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

    /// <summary>
    /// 判断缓存键是否存在
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public bool Exists(string key, bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.Exists(key, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步判断缓存键是否存在
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<bool> ExistsAsync(string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.ExistsAsync(key, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 按模式获取缓存键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public string[] GetKeys(string pattern = "*", bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.GetKeys(pattern, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步按模式获取缓存键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<string[]> GetKeysAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.GetKeysAsync(pattern, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 按模式移除缓存键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public long RemoveByPattern(string pattern = "*", bool? hideErrors = null, bool considerUow = false)
    {
        return InternalCache.RemoveByPattern(pattern, hideErrors, considerUow);
    }

    /// <summary>
    /// 异步按模式移除缓存键
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task<long> RemoveByPatternAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return InternalCache.RemoveByPatternAsync(pattern, hideErrors, considerUow, token);
    }

}

/// <summary>
/// 分布式缓存
/// </summary>
/// <typeparam name="TCacheItem">缓存的缓存项的类型</typeparam>
/// <typeparam name="TCacheKey">缓存键的类型</typeparam>
public class DistributedCache<TCacheItem, TCacheKey> : IDistributedCache<TCacheItem, TCacheKey>
    where TCacheItem : class
    where TCacheKey : notnull
{
    /// <summary>
    /// 缓存名称
    /// </summary>
    public const string UowCacheName = "XiHanDistributedCache";

    /// <summary>
    /// 默认缓存条目选项
    /// </summary>
    protected DistributedCacheEntryOptions DefaultCacheOptions = null!;

    private readonly XiHanDistributedCacheOptions _distributedCacheOption;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="distributedCacheOption"></param>
    /// <param name="cache"></param>
    /// <param name="cancellationTokenProvider"></param>
    /// <param name="serializer"></param>
    /// <param name="keyNormalizer"></param>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="unitOfWorkManager"></param>
    public DistributedCache(
        IOptions<XiHanDistributedCacheOptions> distributedCacheOption,
        IDistributedCache cache,
        ICancellationTokenProvider cancellationTokenProvider,
        IDistributedCacheSerializer serializer,
        IDistributedCacheKeyNormalizer keyNormalizer,
        IServiceScopeFactory serviceScopeFactory,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _distributedCacheOption = distributedCacheOption.Value;
        Cache = cache;
        CancellationTokenProvider = cancellationTokenProvider;
        Logger = NullLogger<DistributedCache<TCacheItem, TCacheKey>>.Instance;
        Serializer = serializer;
        KeyNormalizer = keyNormalizer;
        ServiceScopeFactory = serviceScopeFactory;
        UnitOfWorkManager = unitOfWorkManager;
        SyncSemaphore = new SemaphoreSlim(1, 1);
        SetDefaultOptions();
    }

    /// <summary>
    /// 日志记录器
    /// </summary>
    public ILogger<DistributedCache<TCacheItem, TCacheKey>> Logger { get; set; }

    /// <summary>
    /// 缓存名称
    /// </summary>
    protected string CacheName { get; set; } = null!;

    /// <summary>
    /// 是否忽略多租户
    /// </summary>
    protected bool IgnoreMultiTenancy { get; set; }

    /// <summary>
    /// 缓存
    /// </summary>
    protected IDistributedCache Cache { get; }

    /// <summary>
    /// 取消令牌提供程序
    /// </summary>
    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    /// <summary>
    /// 序列化器
    /// </summary>
    protected IDistributedCacheSerializer Serializer { get; }

    /// <summary>
    /// 缓存键规范化器
    /// </summary>
    protected IDistributedCacheKeyNormalizer KeyNormalizer { get; }

    /// <summary>
    /// 服务作用域工厂
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 同步信号量
    /// </summary>
    protected SemaphoreSlim SyncSemaphore { get; }

    /// <summary>
    /// 获取缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public virtual TCacheItem? Get(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        if (ShouldConsiderUow(considerUow))
        {
            var value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
            if (value is not null)
            {
                return value;
            }
        }

        byte[]? cachedBytes;
        try
        {
            cachedBytes = Cache.Get(NormalizeKey(key));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return null;
        }

        return ToCacheItem(cachedBytes);
    }

    /// <summary>
    /// 获取多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow">确认缓存</param>
    /// <returns></returns>
    public virtual KeyValuePair<TCacheKey, TCacheItem?>[] GetMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false)
    {
        var keyArray = keys.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            return GetManyFallback(keyArray, hideErrors, considerUow);
        }

        var notCachedKeys = new List<TCacheKey>();
        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var key in keyArray)
            {
                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                if (value is not null)
                {
                    cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
                }
            }

            notCachedKeys = [.. keyArray.Except(cachedValues.Select(x => x.Key))];
            if (notCachedKeys.Count == 0)
            {
                return [.. cachedValues];
            }
        }

        hideErrors ??= _distributedCacheOption.HideErrors;
        byte[]?[] cachedBytes;
        var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
        try
        {
            cachedBytes = cacheSupportsMultipleItems.GetMany(readKeys.Select(NormalizeKey));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return ToCacheItemsWithDefaultValues(keyArray);
        }

        return AlignWithRequestedKeys(keyArray, cachedValues, ToCacheItems(cachedBytes, readKeys));
    }

    /// <summary>
    /// 异步获取多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        var keyArray = keys.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            return await GetManyFallbackAsync(keyArray, hideErrors, considerUow, token);
        }

        var notCachedKeys = new List<TCacheKey>();
        var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var key in keyArray)
            {
                var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                if (value is not null)
                {
                    cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
                }
            }

            notCachedKeys = [.. keyArray.Except(cachedValues.Select(x => x.Key))];
            if (notCachedKeys.Count == 0)
            {
                return [.. cachedValues];
            }
        }

        hideErrors ??= _distributedCacheOption.HideErrors;
        byte[]?[] cachedBytes;
        var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
        try
        {
            cachedBytes = await cacheSupportsMultipleItems.GetManyAsync(readKeys.Select(NormalizeKey), CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return ToCacheItemsWithDefaultValues(keyArray);
        }

        return AlignWithRequestedKeys(keyArray, cachedValues, ToCacheItems(cachedBytes, readKeys));
    }

    /// <summary>
    /// 异步获取缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<TCacheItem?> GetAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        if (ShouldConsiderUow(considerUow))
        {
            var value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
            if (value is not null)
            {
                return value;
            }
        }

        byte[]? cachedBytes;
        try
        {
            cachedBytes = await Cache.GetAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return null;
        }

        return cachedBytes is null ? null : Serializer.Deserialize<TCacheItem>(cachedBytes);
    }

    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public virtual TCacheItem? GetOrAdd(TCacheKey key, Func<TCacheItem> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
    {
        var value = Get(key, hideErrors, considerUow);
        if (value is not null)
        {
            return value;
        }

        using (SyncSemaphore.Lock())
        {
            value = Get(key, hideErrors, considerUow);
            if (value is not null)
            {
                return value;
            }

            value = factory();
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                if (uowCache.TryGetValue(key, out var item))
                {
                    item.SetValue(value);
                }
                else
                {
                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
                }
            }

            Set(key, value, optionsFactory?.Invoke(), hideErrors, considerUow);
        }

        return value;
    }

    /// <summary>
    /// 异步获取或添加缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<TCacheItem?> GetOrAddAsync(TCacheKey key, Func<Task<TCacheItem>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        token = CancellationTokenProvider.FallbackToProvider(token);
        var value = await GetAsync(key, hideErrors, considerUow, token);
        if (value is not null)
        {
            return value;
        }

        using (await SyncSemaphore.LockAsync(token))
        {
            value = await GetAsync(key, hideErrors, considerUow, token);
            if (value is not null)
            {
                return value;
            }

            value = await factory();
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                if (uowCache.TryGetValue(key, out var item))
                {
                    item.SetValue(value);
                }
                else
                {
                    uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
                }
            }

            await SetAsync(key, value, optionsFactory?.Invoke(), hideErrors, considerUow, token);
        }

        return value;
    }

    /// <summary>
    /// 获取多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public KeyValuePair<TCacheKey, TCacheItem?>[] GetOrAddMany(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, List<KeyValuePair<TCacheKey, TCacheItem>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false)
    {
        KeyValuePair<TCacheKey, TCacheItem?>[] result;
        var keyArray = keys.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            result = GetManyFallback(keyArray, hideErrors, considerUow);
        }
        else
        {
            var notCachedKeys = new List<TCacheKey>();
            var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                foreach (var key in keyArray)
                {
                    var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                    if (value is not null)
                    {
                        cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
                    }
                }

                notCachedKeys = [.. keyArray.Except(cachedValues.Select(x => x.Key))];
                if (notCachedKeys.Count == 0)
                {
                    return [.. cachedValues];
                }
            }

            hideErrors ??= _distributedCacheOption.HideErrors;
            byte[]?[] cachedBytes;
            var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
            try
            {
                cachedBytes = cacheSupportsMultipleItems.GetMany(readKeys.Select(NormalizeKey));
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                HandleException(ex);
                return ToCacheItemsWithDefaultValues(keyArray);
            }

            result = AlignWithRequestedKeys(keyArray, cachedValues, ToCacheItems(cachedBytes, readKeys));
        }

        if (result.All(x => x.Value is not null))
        {
            return result;
        }

        var missingKeys = new List<TCacheKey>();
        var missingValuesIndex = new List<int>();
        for (var i = 0; i < keyArray.Length; i++)
        {
            if (result[i].Value is not null)
            {
                continue;
            }

            missingKeys.Add(keyArray[i]);
            missingValuesIndex.Add(i);
        }

        var missingValues = factory.Invoke(missingKeys).ToArray();
        var valueQueue = new Queue<KeyValuePair<TCacheKey, TCacheItem>>(missingValues);
        SetMany(missingValues, optionsFactory?.Invoke(), hideErrors, considerUow);
        foreach (var index in missingValuesIndex)
        {
            result[index] = valueQueue.Dequeue()!;
        }

        return result;
    }

    /// <summary>
    /// 异步获取或添加多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="factory"></param>
    /// <param name="optionsFactory"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetOrAddManyAsync(IEnumerable<TCacheKey> keys, Func<IEnumerable<TCacheKey>, Task<List<KeyValuePair<TCacheKey, TCacheItem>>>> factory, Func<DistributedCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        KeyValuePair<TCacheKey, TCacheItem?>[] result;
        var keyArray = keys.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            result = await GetManyFallbackAsync(keyArray, hideErrors, considerUow, token);
        }
        else
        {
            var notCachedKeys = new List<TCacheKey>();
            var cachedValues = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                foreach (var key in keyArray)
                {
                    var value = uowCache.GetOrDefault(key)?.GetUnRemovedValueOrNull();
                    if (value is not null)
                    {
                        cachedValues.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, value));
                    }
                }

                notCachedKeys = [.. keyArray.Except(cachedValues.Select(x => x.Key))];
                if (notCachedKeys.Count == 0)
                {
                    return [.. cachedValues];
                }
            }

            hideErrors ??= _distributedCacheOption.HideErrors;
            byte[]?[] cachedBytes;
            var readKeys = notCachedKeys.Count != 0 ? [.. notCachedKeys] : keyArray;
            try
            {
                cachedBytes = await cacheSupportsMultipleItems.GetManyAsync(readKeys.Select(NormalizeKey), token);
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                await HandleExceptionAsync(ex);
                return ToCacheItemsWithDefaultValues(keyArray);
            }

            result = AlignWithRequestedKeys(keyArray, cachedValues, ToCacheItems(cachedBytes, readKeys));
        }

        if (result.All(x => x.Value is not null))
        {
            return result;
        }

        var missingKeys = new List<TCacheKey>();
        var missingValuesIndex = new List<int>();
        for (var i = 0; i < keyArray.Length; i++)
        {
            if (result[i].Value is not null)
            {
                continue;
            }

            missingKeys.Add(keyArray[i]);
            missingValuesIndex.Add(i);
        }

        var missingValues = (await factory.Invoke(missingKeys)).ToArray();
        var valueQueue = new Queue<KeyValuePair<TCacheKey, TCacheItem>>(missingValues);
        await SetManyAsync(missingValues, optionsFactory?.Invoke(), hideErrors, considerUow, token);
        foreach (var index in missingValuesIndex)
        {
            result[index] = valueQueue.Dequeue()!;
        }

        return result;
    }

    /// <summary>
    /// 设置缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public virtual void Set(TCacheKey key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
    {
        void SetRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                Cache.Set(NormalizeKey(key), Serializer.Serialize(value), options ?? DefaultCacheOptions);
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                HandleException(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].SetValue(value);
            }
            else
            {
                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
            }

            UnitOfWorkManager.Current?.OnCompleted(() =>
            {
                SetRealCache();
                return Task.CompletedTask;
            });
        }
        else
        {
            SetRealCache();
        }
    }

    /// <summary>
    /// 异步设置缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task SetAsync(TCacheKey key, TCacheItem value, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        async Task SetRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                await Cache.SetAsync(NormalizeKey(key), Serializer.Serialize(value), options ?? DefaultCacheOptions, CancellationTokenProvider.FallbackToProvider(token));
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                await HandleExceptionAsync(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].SetValue(value);
            }
            else
            {
                uowCache.Add(key, new UnitOfWorkCacheItem<TCacheItem>(value));
            }

            UnitOfWorkManager.Current?.OnCompleted(SetRealCache);
        }
        else
        {
            await SetRealCache();
        }
    }

    /// <summary>
    /// 设置多个缓存项
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void SetMany(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
    {
        var itemsArray = items.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            SetManyFallback(itemsArray, options, hideErrors, considerUow);
            return;
        }

        void SetRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                cacheSupportsMultipleItems.SetMany(ToRawCacheItems(itemsArray), options ?? DefaultCacheOptions);
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                HandleException(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var pair in itemsArray)
            {
                if (uowCache.TryGetValue(pair.Key, out _))
                {
                    uowCache[pair.Key].SetValue(pair.Value);
                }
                else
                {
                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
                }
            }

            UnitOfWorkManager.Current?.OnCompleted(() =>
            {
                SetRealCache();
                return Task.CompletedTask;
            });
        }
        else
        {
            SetRealCache();
        }
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
    public virtual async Task SetManyAsync(IEnumerable<KeyValuePair<TCacheKey, TCacheItem>> items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        var itemsArray = items.ToArray();
        if (Cache is not ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            await SetManyFallbackAsync(itemsArray, options, hideErrors, considerUow, token);
            return;
        }

        async Task SetRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                await cacheSupportsMultipleItems.SetManyAsync(ToRawCacheItems(itemsArray), options ?? DefaultCacheOptions, CancellationTokenProvider.FallbackToProvider(token));
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                await HandleExceptionAsync(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            foreach (var pair in itemsArray)
            {
                if (uowCache.TryGetValue(pair.Key, out _))
                {
                    uowCache[pair.Key].SetValue(pair.Value);
                }
                else
                {
                    uowCache.Add(pair.Key, new UnitOfWorkCacheItem<TCacheItem>(pair.Value));
                }
            }

            UnitOfWorkManager.Current?.OnCompleted(SetRealCache);
        }
        else
        {
            await SetRealCache();
        }
    }

    /// <summary>
    /// 刷新缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    public virtual void Refresh(TCacheKey key, bool? hideErrors = null)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            Cache.Refresh(NormalizeKey(key));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
        }
    }

    /// <summary>
    /// 异步刷新缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RefreshAsync(TCacheKey key, bool? hideErrors = null, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            await Cache.RefreshAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
        }
    }

    /// <summary>
    /// 刷新多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    public virtual void RefreshMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
            {
                cacheSupportsMultipleItems.RefreshMany(keys.Select(NormalizeKey));
            }
            else
            {
                foreach (var key in keys)
                {
                    Cache.Refresh(NormalizeKey(key));
                }
            }
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
        }
    }

    /// <summary>
    /// 异步刷新多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RefreshManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
            {
                await cacheSupportsMultipleItems.RefreshManyAsync(keys.Select(NormalizeKey), token);
            }
            else
            {
                foreach (var key in keys)
                {
                    await Cache.RefreshAsync(NormalizeKey(key), token);
                }
            }
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
        }
    }

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public virtual void Remove(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
    {
        void RemoveRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                Cache.Remove(NormalizeKey(key));
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                HandleException(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].RemoveValue();
            }

            UnitOfWorkManager.Current?.OnCompleted(() =>
            {
                RemoveRealCache();
                return Task.CompletedTask;
            });
        }
        else
        {
            RemoveRealCache();
        }
    }

    /// <summary>
    /// 异步移除缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RemoveAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        async Task RemoveRealCache()
        {
            hideErrors ??= _distributedCacheOption.HideErrors;
            try
            {
                await Cache.RemoveAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token));
            }
            catch (Exception ex)
            {
                if (hideErrors != true)
                {
                    throw;
                }

                await HandleExceptionAsync(ex);
            }
        }

        if (ShouldConsiderUow(considerUow))
        {
            var uowCache = GetUnitOfWorkCache();
            if (uowCache.TryGetValue(key, out _))
            {
                uowCache[key].RemoveValue();
            }

            UnitOfWorkManager.Current?.OnCompleted(RemoveRealCache);
        }
        else
        {
            await RemoveRealCache();
        }
    }

    /// <summary>
    /// 移除多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    public void RemoveMany(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false)
    {
        var keyArray = keys.ToArray();
        if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            void RemoveRealCache()
            {
                hideErrors ??= _distributedCacheOption.HideErrors;
                try
                {
                    cacheSupportsMultipleItems.RemoveMany(keyArray.Select(NormalizeKey));
                }
                catch (Exception ex)
                {
                    if (hideErrors != true)
                    {
                        throw;
                    }

                    HandleException(ex);
                }
            }

            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                foreach (var key in keyArray)
                {
                    if (uowCache.TryGetValue(key, out _))
                    {
                        uowCache[key].RemoveValue();
                    }
                }

                UnitOfWorkManager.Current?.OnCompleted(() =>
                {
                    RemoveRealCache();
                    return Task.CompletedTask;
                });
            }
            else
            {
                RemoveRealCache();
            }
        }
        else
        {
            foreach (var key in keyArray)
            {
                Remove(key, hideErrors, considerUow);
            }
        }
    }

    /// <summary>
    /// 异步移除多个缓存
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task RemoveManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        var keyArray = keys.ToArray();
        if (Cache is ICacheSupportsMultipleItems cacheSupportsMultipleItems)
        {
            async Task RemoveRealCache()
            {
                hideErrors ??= _distributedCacheOption.HideErrors;
                try
                {
                    await cacheSupportsMultipleItems.RemoveManyAsync(keyArray.Select(NormalizeKey), token);
                }
                catch (Exception ex)
                {
                    if (hideErrors != true)
                    {
                        throw;
                    }

                    await HandleExceptionAsync(ex);
                }
            }

            if (ShouldConsiderUow(considerUow))
            {
                var uowCache = GetUnitOfWorkCache();
                foreach (var key in keyArray)
                {
                    if (uowCache.TryGetValue(key, out _))
                    {
                        uowCache[key].RemoveValue();
                    }
                }

                UnitOfWorkManager.Current?.OnCompleted(RemoveRealCache);
            }
            else
            {
                await RemoveRealCache();
            }
        }
        else
        {
            foreach (var key in keyArray)
            {
                await RemoveAsync(key, hideErrors, considerUow, token);
            }
        }
    }

    /// <summary>
    /// 判断缓存键是否存在
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public virtual bool Exists(TCacheKey key, bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;

        if (ShouldConsiderUow(considerUow))
        {
            var uowItem = GetUnitOfWorkCache().GetOrDefault(key);
            if (uowItem is not null)
            {
                return !uowItem.IsRemoved && uowItem.Value is not null;
            }
        }

        try
        {
            return Cache.Get(NormalizeKey(key)) is not null;
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return false;
        }
    }

    /// <summary>
    /// 异步判断缓存键是否存在
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<bool> ExistsAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;

        if (ShouldConsiderUow(considerUow))
        {
            var uowItem = GetUnitOfWorkCache().GetOrDefault(key);
            if (uowItem is not null)
            {
                return !uowItem.IsRemoved && uowItem.Value is not null;
            }
        }

        try
        {
            return await Cache.GetAsync(NormalizeKey(key), CancellationTokenProvider.FallbackToProvider(token)) is not null;
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return false;
        }
    }

    /// <summary>
    /// 按模式获取缓存键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public virtual TCacheKey[] GetKeys(string pattern = "*", bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        var businessPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            EnsureStringCacheKeyType();

            var keys = Cache is ICacheSupportsKeyPattern keyPatternSupports
                ? keyPatternSupports.GetKeys(NormalizePattern(businessPattern))
                : [];
            var businessKeys = ToBusinessKeys(keys);
            if (ShouldConsiderUow(considerUow))
            {
                businessKeys = MergeWithUnitOfWorkKeys(businessKeys, businessPattern);
            }

            return [.. businessKeys.Select(static key => (TCacheKey)(object)key)];
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return [];
        }
    }

    /// <summary>
    /// 异步按模式获取缓存键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<TCacheKey[]> GetKeysAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        token = CancellationTokenProvider.FallbackToProvider(token);
        var businessPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            EnsureStringCacheKeyType();

            var keys = Cache is ICacheSupportsKeyPattern keyPatternSupports
                ? await keyPatternSupports.GetKeysAsync(NormalizePattern(businessPattern), token)
                : [];
            var businessKeys = ToBusinessKeys(keys);
            if (ShouldConsiderUow(considerUow))
            {
                businessKeys = MergeWithUnitOfWorkKeys(businessKeys, businessPattern);
            }

            return [.. businessKeys.Select(static key => (TCacheKey)(object)key)];
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return [];
        }
    }

    /// <summary>
    /// 按模式移除缓存键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    public virtual long RemoveByPattern(string pattern = "*", bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        var businessPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            EnsureStringCacheKeyType();
            if (!ShouldConsiderUow(considerUow) && Cache is ICacheSupportsKeyPattern keyPatternSupports)
            {
                return keyPatternSupports.RemoveByPattern(NormalizePattern(businessPattern));
            }

            var keys = GetKeys(businessPattern, hideErrors, considerUow);
            if (keys.Length == 0)
            {
                return 0;
            }

            RemoveMany(keys, hideErrors, considerUow);
            return keys.LongLength;
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return 0;
        }
    }

    /// <summary>
    /// 异步按模式移除缓存键（仅支持字符串键缓存）
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<long> RemoveByPatternAsync(string pattern = "*", bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        token = CancellationTokenProvider.FallbackToProvider(token);
        var businessPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            EnsureStringCacheKeyType();
            if (!ShouldConsiderUow(considerUow) && Cache is ICacheSupportsKeyPattern keyPatternSupports)
            {
                return await keyPatternSupports.RemoveByPatternAsync(NormalizePattern(businessPattern), token);
            }

            var keys = await GetKeysAsync(businessPattern, hideErrors, considerUow, token);
            if (keys.Length == 0)
            {
                return 0;
            }

            await RemoveManyAsync(keys, hideErrors, considerUow, token);
            return keys.LongLength;
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return 0;
        }
    }

    /// <summary>
    /// 执行 Lua 脚本
    /// </summary>
    /// <param name="script"></param>
    /// <param name="keys"></param>
    /// <param name="values"></param>
    /// <param name="hideErrors"></param>
    /// <returns></returns>
    public virtual CacheScriptResult? ScriptEvaluate(string script, IEnumerable<TCacheKey>? keys = null, object?[]? values = null, bool? hideErrors = null)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;

        try
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentException("Lua 脚本不能为空。", nameof(script));
            }

            if (Cache is not ICacheSupportsLuaScript cacheSupportsLuaScript)
            {
                throw new NotSupportedException("当前缓存实现不支持 Lua 脚本执行。");
            }

            var normalizedKeys = keys?.Select(NormalizeKey).ToArray() ?? [];
            var args = values ?? [];
            return cacheSupportsLuaScript.ScriptEvaluate(script, normalizedKeys, args);
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return null;
        }
    }

    /// <summary>
    /// 异步执行 Lua 脚本
    /// </summary>
    /// <param name="script"></param>
    /// <param name="keys"></param>
    /// <param name="values"></param>
    /// <param name="hideErrors"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<CacheScriptResult?> ScriptEvaluateAsync(string script, IEnumerable<TCacheKey>? keys = null, object?[]? values = null, bool? hideErrors = null, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        token = CancellationTokenProvider.FallbackToProvider(token);

        try
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentException("Lua 脚本不能为空。", nameof(script));
            }

            if (Cache is not ICacheSupportsLuaScript cacheSupportsLuaScript)
            {
                throw new NotSupportedException("当前缓存实现不支持 Lua 脚本执行。");
            }

            var normalizedKeys = keys?.Select(NormalizeKey).ToArray() ?? [];
            var args = values ?? [];
            return await cacheSupportsLuaScript.ScriptEvaluateAsync(script, normalizedKeys, args, token);
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return null;
        }
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    protected virtual string NormalizeKey(TCacheKey key)
    {
        return ApplyKeyPrefix(KeyNormalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs(key.ToString()!, CacheName, IgnoreMultiTenancy)));
    }

    /// <summary>
    /// 规范化模式键
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    protected virtual string NormalizePattern(string pattern)
    {
        return ApplyKeyPrefix(KeyNormalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs(pattern, CacheName, IgnoreMultiTenancy)));
    }

    /// <summary>
    /// 获取规范化键前缀
    /// </summary>
    /// <returns></returns>
    protected virtual string GetNormalizedKeyPrefix()
    {
        return ApplyKeyPrefix(KeyNormalizer.NormalizeKey(new DistributedCacheKeyNormalizeArgs(string.Empty, CacheName, IgnoreMultiTenancy)));
    }

    /// <summary>
    /// 在规范化键外层拼上选项里配置的应用级键前缀
    /// </summary>
    /// <param name="normalizedKey">键规范化器产出的「租户段:缓存名段:业务键」</param>
    /// <returns>最终写入后端的键</returns>
    /// <remarks>
    /// <see cref="XiHanDistributedCacheOptions.KeyPrefix"/> 原先全仓没有任何读取点，配了等于没配，
    /// 多个应用共用同一个 Redis 实例时无法靠它做隔离。这里把前缀统一收在本方法里，
    /// 是因为它必须同时作用于「写键」「查键模式」「剥前缀还原业务键」三条路径：
    /// 只在 NormalizeKey 上拼，GetKeys 取回来的键就剥不掉前缀，按模式清理会误伤。
    /// 前缀默认为空串，不配置时产出的键与改动前逐字节一致。
    /// </remarks>
    protected virtual string ApplyKeyPrefix(string normalizedKey)
    {
        return _distributedCacheOption.KeyPrefix + normalizedKey;
    }

    /// <summary>
    /// 获取默认缓存条目选项
    /// </summary>
    /// <returns></returns>
    protected virtual DistributedCacheEntryOptions GetDefaultCacheEntryOptions()
    {
        foreach (var configure in _distributedCacheOption.CacheConfigurators)
        {
            var options = configure.Configure(CacheName);
            if (options is not null)
            {
                return options;
            }
        }

        return _distributedCacheOption.GlobalCacheEntryOptions;
    }

    /// <summary>
    /// 设置默认选项
    /// </summary>
    protected virtual void SetDefaultOptions()
    {
        CacheName = CacheNameAttribute.GetCacheName<TCacheItem>();

        //IgnoreMultiTenancy
        IgnoreMultiTenancy = typeof(TCacheItem).IsDefined(typeof(IgnoreMultiTenancyAttribute), true);

        //Configure default cache entry options
        DefaultCacheOptions = GetDefaultCacheEntryOptions();
    }

    /// <summary>
    /// 获取多个回滚
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow">确认缓存</param>
    /// <returns></returns>
    protected virtual KeyValuePair<TCacheKey, TCacheItem?>[] GetManyFallback(TCacheKey[] keys, bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            return [.. keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem?>(key, Get(key, false, considerUow)))];
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
            return ToCacheItemsWithDefaultValues(keys);
        }
    }

    /// <summary>
    /// 异步获取多个回滚
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual async Task<KeyValuePair<TCacheKey, TCacheItem?>[]> GetManyFallbackAsync(TCacheKey[] keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            var result = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
            foreach (var key in keys)
            {
                result.Add(new KeyValuePair<TCacheKey, TCacheItem?>(key, await GetAsync(key, false, considerUow, token)));
            }

            return [.. result];
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
            return ToCacheItemsWithDefaultValues(keys);
        }
    }

    /// <summary>
    /// 设置多个回滚
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    protected virtual void SetManyFallback(KeyValuePair<TCacheKey, TCacheItem>[] items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            foreach (var item in items)
            {
                Set(item.Key, item.Value, options, false, considerUow);
            }
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            HandleException(ex);
        }
    }

    /// <summary>
    /// 异步设置多个回滚
    /// </summary>
    /// <param name="items"></param>
    /// <param name="options"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected virtual async Task SetManyFallbackAsync(KeyValuePair<TCacheKey, TCacheItem>[] items, DistributedCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        hideErrors ??= _distributedCacheOption.HideErrors;
        try
        {
            foreach (var item in items)
            {
                await SetAsync(item.Key, item.Value, options, false, considerUow, token);
            }
        }
        catch (Exception ex)
        {
            if (hideErrors != true)
            {
                throw;
            }

            await HandleExceptionAsync(ex);
        }
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    /// <param name="ex"></param>
    protected virtual void HandleException(Exception ex)
    {
        HandleExceptionAsync(ex).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步处理异常
    /// </summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    protected virtual async Task HandleExceptionAsync(Exception ex)
    {
        Logger.LogException(ex, LogLevel.Warning);
        using var scope = ServiceScopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(ex, LogLevel.Warning));
    }

    /// <summary>
    /// 转换为缓存项
    /// </summary>
    /// <param name="itemBytes"></param>
    /// <param name="itemKeys"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    protected virtual KeyValuePair<TCacheKey, TCacheItem?>[] ToCacheItems(byte[]?[] itemBytes, TCacheKey[] itemKeys)
    {
        if (itemBytes.Length != itemKeys.Length)
        {
            throw new XiHanException("项目字节数应与给定键的数量相同");
        }

        var result = new List<KeyValuePair<TCacheKey, TCacheItem?>>();
        for (var i = 0; i < itemKeys.Length; i++)
        {
            result.Add(new KeyValuePair<TCacheKey, TCacheItem?>(itemKeys[i], ToCacheItem(itemBytes[i])));
        }

        return [.. result];
    }

    /// <summary>
    /// 转化为缓存项
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    protected virtual TCacheItem? ToCacheItem(byte[]? bytes)
    {
        return bytes is null ? null : Serializer.Deserialize<TCacheItem>(bytes);
    }

    /// <summary>
    /// 转化缓存项为原始缓存项
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    protected virtual KeyValuePair<string, byte[]>[] ToRawCacheItems(KeyValuePair<TCacheKey, TCacheItem>[] items)
    {
        return [.. items.Select(i => new KeyValuePair<string, byte[]>(NormalizeKey(i.Key), Serializer.Serialize(i.Value)))];
    }

    /// <summary>
    /// 是否应该确认 UOW
    /// </summary>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    protected virtual bool ShouldConsiderUow(bool considerUow)
    {
        return considerUow && UnitOfWorkManager.Current is not null;
    }

    /// <summary>
    /// 获取 UOW 缓存键
    /// </summary>
    /// <returns></returns>
    protected virtual string GetUnitOfWorkCacheKey()
    {
        return UowCacheName + CacheName;
    }

    /// <summary>
    /// 获取 UOW 缓存
    /// </summary>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    protected virtual Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>> GetUnitOfWorkCache()
    {
        return UnitOfWorkManager.Current is null
            ? throw new XiHanException("没有活跃的 UOW")
            : UnitOfWorkManager.Current.GetOrAddItem(GetUnitOfWorkCacheKey(), key => new Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>>());
    }

    /// <summary>
    /// 按入参键的原始顺序装配批量读取结果
    /// </summary>
    /// <param name="keyArray">调用方传入的键，结果按其顺序逐位对齐</param>
    /// <param name="cachedValues">命中工作单元缓存的项</param>
    /// <param name="fetchedValues">从缓存后端读回的项</param>
    /// <returns>与 <paramref name="keyArray"/> 严格按位对应的结果</returns>
    /// <remarks>
    /// 原来这里是 <c>[.. cachedValues, .. fetchedValues]</c> 直接拼接：
    /// considerUow=true 且只有部分键命中工作单元缓存时，结果被重排成「先已命中、后未命中」，
    /// 不再与入参 keyArray 同序。而 GetOrAddMany 随后按 <c>result[i]</c> 是否为空去取 <c>keyArray[i]</c>
    /// 当作缺失键，两边下标错位，会把已命中的键当缺失键交给工厂，再把工厂结果写回别的键的槽位，
    /// 最终某个入参键会整个从结果里消失。这也违反 IDistributedCache 接口注释里
    /// 「返回的列表与提供的键数量相同（且按位对应）」的约定。
    /// 改为先建 key→value 映射再按 keyArray 投影，顺便也让入参含重复键时长度保持等于 keyArray.Length。
    /// </remarks>
    private static KeyValuePair<TCacheKey, TCacheItem?>[] AlignWithRequestedKeys(
        TCacheKey[] keyArray,
        List<KeyValuePair<TCacheKey, TCacheItem?>> cachedValues,
        KeyValuePair<TCacheKey, TCacheItem?>[] fetchedValues)
    {
        var valueMap = new Dictionary<TCacheKey, TCacheItem?>();
        foreach (var pair in cachedValues)
        {
            valueMap[pair.Key] = pair.Value;
        }

        foreach (var pair in fetchedValues)
        {
            valueMap[pair.Key] = pair.Value;
        }

        var aligned = new KeyValuePair<TCacheKey, TCacheItem?>[keyArray.Length];
        for (var i = 0; i < keyArray.Length; i++)
        {
            valueMap.TryGetValue(keyArray[i], out var value);
            aligned[i] = new KeyValuePair<TCacheKey, TCacheItem?>(keyArray[i], value);
        }

        return aligned;
    }

    /// <summary>
    /// 转化缓存项为默认值
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    private static KeyValuePair<TCacheKey, TCacheItem?>[] ToCacheItemsWithDefaultValues(TCacheKey[] keys)
    {
        return [.. keys.Select(key => new KeyValuePair<TCacheKey, TCacheItem?>(key, null))];
    }

    /// <summary>
    /// 确保键类型为字符串
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    private static void EnsureStringCacheKeyType()
    {
        if (typeof(TCacheKey) != typeof(string))
        {
            throw new NotSupportedException($"当前缓存键类型 '{typeof(TCacheKey).Name}' 不支持按模式操作，需使用 string 键类型。");
        }
    }

    /// <summary>
    /// 创建通配符匹配正则
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private static Regex CreateWildcardRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return new Regex(regexPattern, RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// 转换为业务键
    /// </summary>
    /// <param name="normalizedKeys"></param>
    /// <returns></returns>
    private string[] ToBusinessKeys(IEnumerable<string> normalizedKeys)
    {
        var keyPrefix = GetNormalizedKeyPrefix();

        return
        [
            .. normalizedKeys
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.StartsWith(keyPrefix, StringComparison.Ordinal)
                    ? key[keyPrefix.Length..]
                    : key)
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// 合并当前工作单元中的键变更
    /// </summary>
    /// <param name="storeKeys"></param>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private string[] MergeWithUnitOfWorkKeys(IEnumerable<string> storeKeys, string pattern)
    {
        var keySet = storeKeys.ToHashSet(StringComparer.Ordinal);
        var regex = CreateWildcardRegex(pattern);
        var uowCache = GetUnitOfWorkCache();

        foreach (var item in uowCache)
        {
            var key = item.Key.ToString()!;
            if (!regex.IsMatch(key))
            {
                continue;
            }

            if (item.Value.IsRemoved || item.Value.Value is null)
            {
                keySet.Remove(key);
            }
            else
            {
                keySet.Add(key);
            }
        }

        return [.. keySet];
    }
}
