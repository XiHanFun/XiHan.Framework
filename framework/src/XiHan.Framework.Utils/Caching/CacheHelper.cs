#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheHelper
// Guid:a8c5d7f1-b9e2-4c3a-8f4d-1234567890ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存帮助类
/// 提供简单易用的内存缓存操作方法，支持国际化的 DateTimeOffset
/// 可直接使用，无需初始化或依赖注入，支持泛型缓存项
/// </summary>
public static class CacheHelper
{
    /// <summary>
    /// 定时清理器，用于定期清理过期的缓存项
    /// </summary>
    private static readonly Timer CleanupTimer;

    /// <summary>
    /// 主缓存存储，用于存储所有缓存项
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> Cache = new();

    /// <summary>
    /// 键级锁，保证同键的初始化/写入是原子的
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> KeyLocks = new();

    /// <summary>
    /// 异步键级锁，用于异步操作的并发控制
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AsyncKeyLocks = new();

    /// <summary>
    /// 静态构造函数，初始化清理定时器
    /// </summary>
    static CacheHelper()
    {
        // 每分钟清理一次过期的缓存项
        CleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    #region 基础缓存操作

    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <returns>缓存值，如果不存在则返回 default(T)</returns>
    public static T? Get<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return default;
        }

        if (Cache.TryGetValue(key, out var cacheObject) && TryGetTypedCacheItem(cacheObject, out CacheItem<T>? cacheItem) && cacheItem != null)
        {
            if (cacheItem.IsExpired)
            {
                Cache.TryRemove(key, out _);
                return default;
            }

            cacheItem.UpdateLastAccessed();
            return cacheItem.Value;
        }

        return default;
    }

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="expireTime">过期时间（使用 DateTimeOffset）</param>
    public static void Set<T>(string key, T value, DateTimeOffset expireTime)
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            return;
        }

        var cacheItem = new CacheItem<T>(value)
        {
            AbsoluteExpiration = expireTime
        };

        Cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);
    }

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="expireSeconds">过期秒数，默认 3600</param>
    public static void Set<T>(string key, T value, int expireSeconds = 3600)
    {
        Set(key, value, DateTimeOffset.UtcNow.AddSeconds(expireSeconds));
    }

    /// <summary>
    /// 设置缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="expireSpan">过期时间间隔</param>
    public static void Set<T>(string key, T value, TimeSpan expireSpan)
    {
        Set(key, value, DateTimeOffset.UtcNow.Add(expireSpan));
    }

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public static void Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        Cache.TryRemove(key, out _);
        KeyLocks.TryRemove(key, out _);
        if (AsyncKeyLocks.TryRemove(key, out var sem))
        {
            sem.Dispose();
        }
    }

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>如果存在返回 true，否则返回 false</returns>
    public static bool Exists(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (Cache.TryGetValue(key, out var cacheObject) && cacheObject is ICacheItem item)
        {
            if (item.IsExpired)
            {
                Cache.TryRemove(key, out _);
                KeyLocks.TryRemove(key, out _);
                return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">创建缓存值的工厂方法</param>
    /// <param name="expireTime">过期时间（使用 DateTimeOffset）</param>
    /// <returns>缓存值</returns>
    public static T GetOrAdd<T>(string key, Func<T> factory, DateTimeOffset expireTime)
    {
        var lazy = (Lazy<CacheItem<T>>)Cache.GetOrAdd(key, _ =>
            new Lazy<CacheItem<T>>(() =>
            new CacheItem<T>(factory())
            {
                AbsoluteExpiration = expireTime
            },
            LazyThreadSafetyMode.ExecutionAndPublication));

        var item = lazy.Value;
        if (item.IsExpired)
        {
            // 替换过期条目
            var replacement = new Lazy<CacheItem<T>>(() =>
            new CacheItem<T>(factory())
            {
                AbsoluteExpiration = expireTime
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
            Cache.AddOrUpdate(key, replacement, (_, existing) => replacement);
            item = ((Lazy<CacheItem<T>>)Cache[key]).Value;
        }
        item.UpdateLastAccessed();
        return item.Value;
    }

    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">创建缓存值的工厂方法</param>
    /// <param name="expireSeconds">过期秒数</param>
    /// <returns>缓存值</returns>
    public static T GetOrAdd<T>(string key, Func<T> factory, int expireSeconds = 3600)
    {
        return GetOrAdd(key, factory, DateTimeOffset.UtcNow.AddSeconds(expireSeconds));
    }

    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">创建缓存值的工厂方法</param>
    /// <param name="expireSpan">过期时间间隔</param>
    /// <returns>缓存值</returns>
    public static T GetOrAdd<T>(string key, Func<T> factory, TimeSpan expireSpan)
    {
        return GetOrAdd(key, factory, DateTimeOffset.UtcNow.Add(expireSpan));
    }

    /// <summary>
    /// 设置滑动过期缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="slidingExpiration">滑动过期时间</param>
    /// <param name="absoluteExpiration">绝对过期时间（可选）</param>
    public static void SetSliding<T>(string key, T value, TimeSpan slidingExpiration, DateTimeOffset? absoluteExpiration = null)
    {
        if (string.IsNullOrEmpty(key) || value == null)
        {
            return;
        }

        var cacheItem = new CacheItem<T>(value)
        {
            SlidingExpiration = slidingExpiration,
            AbsoluteExpiration = absoluteExpiration
        };

        Cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);
    }

    #endregion

    #region 异步 API

    /// <summary>
    /// 异步获取或添加缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">创建缓存值的工厂方法</param>
    /// <param name="expireTime">过期时间（使用 DateTimeOffset）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存值</returns>
    public static async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, DateTimeOffset expireTime, CancellationToken cancellationToken = default)
    {
        var existing = Get<T>(key);
        if (existing != null)
        {
            return existing;
        }

        var sem = GetAsyncKeyLock(key);
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var doubleChecked = Get<T>(key);
            if (doubleChecked != null)
            {
                return doubleChecked;
            }

            var created = await factory(cancellationToken).ConfigureAwait(false);
            Set(key, created, expireTime);
            return created;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// 异步获取或添加缓存
    /// </summary>
    public static Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, int expireSeconds = 3600, CancellationToken cancellationToken = default)
    {
        return GetOrAddAsync(key, factory, DateTimeOffset.UtcNow.AddSeconds(expireSeconds), cancellationToken);
    }

    /// <summary>
    /// 异步获取或添加缓存
    /// </summary>
    public static Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expireSpan, CancellationToken cancellationToken = default)
    {
        return GetOrAddAsync(key, factory, DateTimeOffset.UtcNow.Add(expireSpan), cancellationToken);
    }

    #endregion

    #region 便利方法

    /// <summary>
    /// 获取缓存项数量
    /// </summary>
    /// <returns>缓存项数量</returns>
    public static int Count => Cache.Count;

    /// <summary>
    /// 尝试获取缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">输出的缓存值</param>
    /// <returns>如果获取成功返回 true，否则返回 false</returns>
    public static bool TryGetValue<T>(string key, out T? value)
    {
        value = default;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (Cache.TryGetValue(key, out var cacheObject) && TryGetTypedCacheItem(cacheObject, out CacheItem<T>? cacheItem) && cacheItem != null)
        {
            if (cacheItem.IsExpired)
            {
                Cache.TryRemove(key, out _);
                KeyLocks.TryRemove(key, out _);
                return false;
            }

            cacheItem.UpdateLastAccessed();
            value = cacheItem.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 批量移除缓存
    /// </summary>
    /// <param name="keys">缓存键集合</param>
    public static void RemoveAll(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            Remove(key);
        }
    }

    /// <summary>
    /// 批量设置缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="items">键值对集合</param>
    /// <param name="expireSeconds">过期秒数，默认 3600</param>
    public static void SetAll<T>(IEnumerable<KeyValuePair<string, T>> items, int expireSeconds = 3600)
    {
        var expireTime = DateTimeOffset.UtcNow.AddSeconds(expireSeconds);
        foreach (var item in items)
        {
            Set(item.Key, item.Value, expireTime);
        }
    }

    /// <summary>
    /// 批量设置缓存
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="items">键值对集合</param>
    /// <param name="expireTime">过期时间（使用 DateTimeOffset）</param>
    public static void SetAll<T>(IEnumerable<KeyValuePair<string, T>> items, DateTimeOffset expireTime)
    {
        foreach (var item in items)
        {
            Set(item.Key, item.Value, expireTime);
        }
    }

    /// <summary>
    /// 获取所有缓存键
    /// </summary>
    /// <returns>所有缓存键的集合</returns>
    public static IEnumerable<string> GetAllKeys()
    {
        return [.. Cache.Keys];
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public static void Clear()
    {
        Cache.Clear();
        KeyLocks.Clear();
        foreach (var pair in AsyncKeyLocks)
        {
            pair.Value.Dispose();
        }
        AsyncKeyLocks.Clear();
    }

    /// <summary>
    /// 手动清理过期的缓存项
    /// </summary>
    public static void CleanupExpired()
    {
        CleanupExpiredItems(null);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 清理过期的缓存项
    /// </summary>
    /// <param name="state">定时器状态</param>
    private static void CleanupExpiredItems(object? state)
    {
        var expiredKeys = new List<string>();

        foreach (var kvp in Cache)
        {
            if (TryGetCacheItem(kvp.Value, out var item) && item.IsExpired)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            Cache.TryRemove(key, out _);
            KeyLocks.TryRemove(key, out _);
            if (AsyncKeyLocks.TryRemove(key, out var sem))
            {
                sem.Dispose();
            }
        }
    }

    /// <summary>
    /// 获取指定键的异步锁
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>异步信号量锁</returns>
    private static SemaphoreSlim GetAsyncKeyLock(string key)
    {
        return AsyncKeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// 尝试从缓存对象中获取缓存项
    /// </summary>
    /// <param name="cacheObject">缓存对象</param>
    /// <param name="item">输出的缓存项</param>
    /// <returns>如果获取成功返回 true，否则返回 false</returns>
    private static bool TryGetCacheItem(object cacheObject, out ICacheItem item)
    {
        item = null!;
        switch (cacheObject)
        {
            case ICacheItem direct:
                item = direct;
                return true;

            case Lazy<ICacheItem> lazyInterface:
                item = lazyInterface.Value;
                return true;

            default:
                {
                    var type = cacheObject.GetType();
                    if (type.IsGenericType)
                    {
                        var genDef = type.GetGenericTypeDefinition();
                        if (genDef == typeof(Lazy<>))
                        {
                            // 处理 Lazy<CacheItem<T>>
                            var valueProp = type.GetProperty("Value");
                            var valueObj = valueProp?.GetValue(cacheObject);
                            if (valueObj is ICacheItem asItem)
                            {
                                item = asItem;
                                return true;
                            }
                        }
                    }
                }
                break;
        }
        return false;
    }

    /// <summary>
    /// 尝试从缓存对象中获取指定类型的缓存项
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="cacheObject">缓存对象</param>
    /// <param name="cacheItem">输出的类型化缓存项</param>
    /// <returns>如果获取成功返回 true，否则返回 false</returns>
    private static bool TryGetTypedCacheItem<T>(object cacheObject, out CacheItem<T>? cacheItem)
    {
        cacheItem = null;
        switch (cacheObject)
        {
            case CacheItem<T> direct:
                cacheItem = direct;
                return true;

            case Lazy<CacheItem<T>> lazyTyped:
                cacheItem = lazyTyped.Value;
                return true;

            default:
                return false;
        }
    }

    #endregion
}
