#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHybridCache
// Guid:3240ca60-d208-48ec-a26f-fc1e5d09f208
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/12 19:46:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using XiHan.Framework.Caching.Extensions;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Exceptions.Handling;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Extensions.Threading;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Threading;
using XiHan.Framework.Threading.Extensions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Extensions;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Caching.Hybrid;

/// <summary>
/// 曦寒混合缓存
/// </summary>
public class XiHanHybridCache<TCacheItem> : IHybridCache<TCacheItem>
    where TCacheItem : class
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="internalCache"></param>
    public XiHanHybridCache(IHybridCache<TCacheItem, string> internalCache)
    {
        InternalCache = internalCache;
    }

    /// <summary>
    /// 内部缓存
    /// </summary>
    public IHybridCache<TCacheItem, string> InternalCache { get; }

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
    public virtual async Task<TCacheItem?> GetOrCreateAsync(string key, Func<Task<TCacheItem>> factory, Func<HybridCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        return await InternalCache.GetOrCreateAsync(key, factory, optionsFactory, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 移除缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RemoveAsync(string key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        await InternalCache.RemoveAsync(key, hideErrors, considerUow, token);
    }

    /// <summary>
    /// 移除多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RemoveManyAsync(IEnumerable<string> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        await InternalCache.RemoveManyAsync(keys, hideErrors, considerUow, token);
    }

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
    public virtual async Task SetAsync(string key, TCacheItem value, HybridCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        await InternalCache.SetAsync(key, value, options, hideErrors, considerUow, token);
    }
}

/// <summary>
/// 曦寒混合缓存
/// </summary>
/// <typeparam name="TCacheItem"></typeparam>
/// <typeparam name="TCacheKey"></typeparam>
public class XiHanHybridCache<TCacheItem, TCacheKey> : IHybridCache<TCacheItem, TCacheKey>
    where TCacheItem : class
    where TCacheKey : notnull
{
    /// <summary>
    /// UOW缓存名称
    /// </summary>
    public const string UowCacheName = "XiHanHybridCache";

    /// <summary>
    /// 默认缓存项选项
    /// </summary>
    protected HybridCacheEntryOptions DefaultCacheOptions = null!;

    /// <summary>
    /// 序列化器缓存
    /// </summary>
    private readonly ConcurrentDictionary<Type, object> _serializersCache = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="distributedCacheOption"></param>
    /// <param name="hybridCache"></param>
    /// <param name="distributedCache"></param>
    /// <param name="cancellationTokenProvider"></param>
    /// <param name="serializer"></param>
    /// <param name="keyNormalizer"></param>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="unitOfWorkManager"></param>
    public XiHanHybridCache(
        IServiceProvider serviceProvider,
        IOptions<XiHanHybridCacheOptions> distributedCacheOption,
        HybridCache hybridCache,
        IDistributedCache distributedCache,
        ICancellationTokenProvider cancellationTokenProvider,
        IDistributedCacheSerializer serializer,
        IDistributedCacheKeyNormalizer keyNormalizer,
        IServiceScopeFactory serviceScopeFactory,
        IUnitOfWorkManager unitOfWorkManager)
    {
        ServiceProvider = serviceProvider;
        DistributedCacheOption = distributedCacheOption.Value;
        HybridCache = hybridCache;
        DistributedCacheCache = distributedCache;
        CancellationTokenProvider = cancellationTokenProvider;
        Logger = NullLogger<XiHanHybridCache<TCacheItem, TCacheKey>>.Instance;
        KeyNormalizer = keyNormalizer;
        ServiceScopeFactory = serviceScopeFactory;
        UnitOfWorkManager = unitOfWorkManager;

        SyncSemaphore = new SemaphoreSlim(1, 1);

        SetDefaultOptions();
    }

    /// <summary>
    /// 日志记录器
    /// </summary>
    public ILogger<XiHanHybridCache<TCacheItem, TCacheKey>> Logger { get; set; }

    /// <summary>
    /// 缓存名称
    /// </summary>
    protected string CacheName { get; set; } = null!;

    /// <summary>
    /// 是否忽略多租户
    /// </summary>
    protected bool IgnoreMultiTenancy { get; set; }

    /// <summary>
    /// 服务提供者
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 混合缓存
    /// </summary>
    protected HybridCache HybridCache { get; }

    /// <summary>
    /// 分布式缓存
    /// </summary>
    protected IDistributedCache DistributedCacheCache { get; }

    /// <summary>
    /// 取消令牌提供者
    /// </summary>
    protected ICancellationTokenProvider CancellationTokenProvider { get; }

    /// <summary>
    /// 序列化器
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
    /// 分布式缓存选项
    /// </summary>
    protected XiHanHybridCacheOptions DistributedCacheOption { get; }

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
    public virtual async Task<TCacheItem?> GetOrCreateAsync(TCacheKey key, Func<Task<TCacheItem>> factory, Func<HybridCacheEntryOptions>? optionsFactory = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        token = CancellationTokenProvider.FallbackToProvider(token);
        hideErrors ??= DistributedCacheOption.HideErrors;

        TCacheItem? value;

        if (!considerUow)
        {
            try
            {
                value = await HybridCache.GetOrCreateAsync(
                    key: NormalizeKey(key),
                    factory: async cancel => await factory(),
                    options: optionsFactory?.Invoke(),
                    tags: null,
                    cancellationToken: token);
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

            return value;
        }

        try
        {
            using (await SyncSemaphore.LockAsync(token))
            {
                if (ShouldConsiderUow(considerUow))
                {
                    value = GetUnitOfWorkCache().GetOrDefault(key)?.GetUnRemovedValueOrNull();
                    if (value is not null)
                    {
                        return value;
                    }
                }

                var bytes = await DistributedCacheCache.GetAsync(NormalizeKey(key), token);
                if (bytes is not null)
                {
                    return ResolveSerializer().Deserialize(new ReadOnlySequence<byte>(bytes, 0, bytes.Length));
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

        return value;
    }

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
    public virtual async Task SetAsync(TCacheKey key, TCacheItem value, HybridCacheEntryOptions? options = null, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        async Task SetRealCache()
        {
            token = CancellationTokenProvider.FallbackToProvider(token);
            hideErrors ??= DistributedCacheOption.HideErrors;

            try
            {
                await HybridCache.SetAsync(
                    key: NormalizeKey(key),
                    value: value,
                    options: options ?? DefaultCacheOptions,
                    tags: null,
                    cancellationToken: token
                );
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
    /// 移除缓存项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task RemoveAsync(TCacheKey key, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        await RemoveManyAsync([key], hideErrors, considerUow, token);
    }

    /// <summary>
    /// 移除多个缓存项
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="hideErrors"></param>
    /// <param name="considerUow"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task RemoveManyAsync(IEnumerable<TCacheKey> keys, bool? hideErrors = null, bool considerUow = false, CancellationToken token = default)
    {
        var keyArray = keys.ToArray();

        async Task RemoveRealCache()
        {
            hideErrors ??= DistributedCacheOption.HideErrors;

            try
            {
                await HybridCache.RemoveAsync(
                    keyArray.Select(NormalizeKey), token);
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

    /// <summary>
    /// 规范化键
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    protected virtual string NormalizeKey(TCacheKey key)
    {
        return KeyNormalizer.NormalizeKey(
            new DistributedCacheKeyNormalizeArgs(
                key.ToString()!,
                CacheName,
                IgnoreMultiTenancy
            )
        );
    }

    /// <summary>
    /// 获取默认缓存项选项
    /// </summary>
    /// <returns></returns>
    protected virtual HybridCacheEntryOptions GetDefaultCacheEntryOptions()
    {
        foreach (var configure in DistributedCacheOption.CacheConfigurators)
        {
            var options = configure.Invoke(CacheName);
            if (options is not null)
            {
                return options;
            }
        }

        return DistributedCacheOption.GlobalHybridCacheEntryOptions;
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
    /// 处理异常
    /// </summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    protected virtual async Task HandleExceptionAsync(Exception ex)
    {
        Logger.LogException(ex, LogLevel.Warning);

        using var scope = ServiceScopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IExceptionNotifier>()
            .NotifyAsync(new ExceptionNotificationContext(ex, LogLevel.Warning));
    }

    /// <summary>
    /// 是否应该考虑UOW
    /// </summary>
    /// <param name="considerUow"></param>
    /// <returns></returns>
    protected virtual bool ShouldConsiderUow(bool considerUow)
    {
        return considerUow && UnitOfWorkManager.Current is not null;
    }

    /// <summary>
    /// 获取UOW缓存键
    /// </summary>
    /// <returns></returns>
    protected virtual string GetUnitOfWorkCacheKey()
    {
        return UowCacheName + CacheName;
    }

    /// <summary>
    /// 获取UOW缓存
    /// </summary>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    protected virtual Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>> GetUnitOfWorkCache()
    {
        return UnitOfWorkManager.Current is null
            ? throw new XiHanException("没有活跃的 UOW。")
            : UnitOfWorkManager.Current.GetOrAddItem(GetUnitOfWorkCacheKey(),
            key => new Dictionary<TCacheKey, UnitOfWorkCacheItem<TCacheItem>>());
    }

    /// <summary>
    /// 解析序列化器
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected virtual IHybridCacheSerializer<TCacheItem> ResolveSerializer()
    {
        if (_serializersCache.TryGetValue(typeof(TCacheItem), out var serializer))
        {
            return serializer.As<IHybridCacheSerializer<TCacheItem>>();
        }

        serializer = ServiceProvider.GetService<IHybridCacheSerializer<TCacheItem>>();
        if (serializer is not null)
        {
            return serializer is null
                ? throw new InvalidOperationException($"在 '{typeof(TCacheItem).Name}' 中没有可用配置 {nameof(IHybridCacheSerializer<TCacheItem>)}")
                : serializer.As<IHybridCacheSerializer<TCacheItem>>();
        }

        var factories = ServiceProvider.GetServices<IHybridCacheSerializerFactory>().ToArray();
        Array.Reverse(factories);
        foreach (var factory in factories)
        {
            if (!factory.TryCreateSerializer<TCacheItem>(out var current))
            {
                continue;
            }

            serializer = current;
            break;
        }

        return serializer is null
            ? throw new InvalidOperationException($"在 '{typeof(TCacheItem).Name}' 中没有可用配置 {nameof(IHybridCacheSerializer<TCacheItem>)}")
            : serializer.As<IHybridCacheSerializer<TCacheItem>>();
    }
}
