#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheHelper
// Guid:a8c5d7f1-b9e2-4c3a-8f4d-1234567890ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 04:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存帮助类
/// 提供简单易用的内存缓存操作方法，支持国际化的 DateTimeOffset
/// 可直接使用，无需初始化或依赖注入，支持泛型缓存项
/// 采用惰性清理 + 队列机制，在读取操作时自动清理过期项
/// 支持 LRU/LFU/FIFO 淘汰策略、统计信息和事件通知
/// </summary>
public static class CacheHelper
{
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
    /// 维护需要检查过期状态的键队列
    /// </summary>
    private static readonly ConcurrentQueue<string> ExpirationQueue = new();

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    private static readonly CacheStatistics Statistics = new();

    /// <summary>
    /// 缓存配置选项
    /// </summary>
    private static readonly CacheOptions Options = new();

    /// <summary>
    /// 缓存事件
    /// </summary>
    public static event EventHandler<CacheEventArgs>? CacheEvent;

    /// <summary>
    /// 惰性清理默认批次大小
    /// </summary>
    private static int CleanupBatchSize => Options.CleanupBatchSize;

    #region 配置和统计

    /// <summary>
    /// 配置缓存选项
    /// </summary>
    /// <param name="configure">配置操作</param>
    public static void Configure(Action<CacheOptions> configure)
    {
        configure?.Invoke(Options);
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>缓存统计信息</returns>
    public static CacheStatistics GetStatistics()
    {
        return Statistics;
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public static void ResetStatistics()
    {
        Statistics.Reset();
    }

    #endregion

    #region 基础缓存操作

    /// <summary>
    /// 获取缓存值
    /// </summary>
    /// <typeparam name="T">缓存值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <returns>缓存值，如果不存在则返回 default(T)</returns>
    public static T? Get<T>(string key)
    {
        // 惰性清理：在读取时触发
        LazyCleanupExpiredItems();

        if (string.IsNullOrEmpty(key))
        {
            if (Options.EnableStatistics)
            {
                Statistics.RecordMiss();
            }
            return default;
        }

        if (Cache.TryGetValue(key, out var cacheObject) && TryGetTypedCacheItem(cacheObject, out CacheItem<T>? cacheItem) && cacheItem != null)
        {
            if (cacheItem.IsExpired)
            {
                Cache.TryRemove(key, out _);
                if (Options.EnableStatistics)
                {
                    Statistics.RecordMiss();
                    Statistics.RecordExpired();
                }
                RaiseEvent(key, CacheEventType.Expired);
                return default;
            }

            cacheItem.UpdateLastAccessed();
            if (Options.EnableStatistics)
            {
                Statistics.RecordHit();
            }
            return cacheItem.Value;
        }

        if (Options.EnableStatistics)
        {
            Statistics.RecordMiss();
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

        // 检查缓存容量，必要时进行淘汰
        EnsureCacheCapacity();

        var cacheItem = new CacheItem<T>(value)
        {
            AbsoluteExpiration = expireTime
        };

        var isNew = !Cache.ContainsKey(key);
        Cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);
        ScheduleExpirationCheck(key);

        RaiseEvent(key, isNew ? CacheEventType.Added : CacheEventType.Updated);
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

        if (Cache.TryRemove(key, out _))
        {
            KeyLocks.TryRemove(key, out _);
            if (AsyncKeyLocks.TryRemove(key, out var sem))
            {
                sem.Dispose();
            }
            RaiseEvent(key, CacheEventType.Removed);
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
            {
                var created = new CacheItem<T>(factory())
                {
                    AbsoluteExpiration = expireTime
                };
                ScheduleExpirationCheck(key);
                return created;
            },
            LazyThreadSafetyMode.ExecutionAndPublication));

        var item = lazy.Value;
        if (item.IsExpired)
        {
            // 替换过期条目
            var replacement = new Lazy<CacheItem<T>>(() =>
            {
                var created = new CacheItem<T>(factory())
                {
                    AbsoluteExpiration = expireTime
                };
                return created;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
            Cache.AddOrUpdate(key, replacement, (_, existing) => replacement);
            item = ((Lazy<CacheItem<T>>)Cache[key]).Value;
            ScheduleExpirationCheck(key);
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
        ScheduleExpirationCheck(key);
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
    public static async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        DateTimeOffset expireTime, CancellationToken cancellationToken = default)
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
    /// 根据模式查询缓存键
    /// </summary>
    /// <param name="pattern">模式字符串，支持通配符 * 和 ?</param>
    /// <returns>匹配的缓存键集合</returns>
    public static IEnumerable<string> GetKeysByPattern(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return [];
        }

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        return [.. Cache.Keys.Where(key => regex.IsMatch(key))];
    }

    /// <summary>
    /// 根据前缀查询缓存键
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <returns>匹配的缓存键集合</returns>
    public static IEnumerable<string> GetKeysByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return [];
        }

        return [.. Cache.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// 根据前缀移除缓存
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <returns>移除的缓存项数量</returns>
    public static int RemoveByPrefix(string prefix)
    {
        var keys = GetKeysByPrefix(prefix);
        var count = 0;
        foreach (var key in keys)
        {
            Remove(key);
            count++;
        }
        return count;
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

        // 清空过期队列
        while (ExpirationQueue.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 手动清理过期的缓存项（全量扫描）
    /// </summary>
    public static void CleanupExpired()
    {
        CleanupExpiredItems();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 触发缓存事件
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="eventType">事件类型</param>
    private static void RaiseEvent(string key, CacheEventType eventType)
    {
        if (Options.EnableEvents && CacheEvent != null)
        {
            var args = new CacheEventArgs
            {
                Key = key,
                EventType = eventType,
                Timestamp = DateTimeOffset.UtcNow
            };
            CacheEvent.Invoke(null, args);
        }
    }

    /// <summary>
    /// 确保缓存容量不超过限制
    /// </summary>
    private static void EnsureCacheCapacity()
    {
        if (Options.MaxCacheSize <= 0 || Cache.Count < Options.MaxCacheSize)
        {
            return;
        }

        // 需要淘汰一些缓存项
        var evictCount = Math.Max(1, Cache.Count - Options.MaxCacheSize + 10); // 多淘汰10个，减少频繁淘汰
        EvictItems(evictCount);
    }

    /// <summary>
    /// 根据策略淘汰缓存项
    /// </summary>
    /// <param name="count">淘汰数量</param>
    private static void EvictItems(int count)
    {
        var items = new List<KeyValuePair<string, ICacheItem>>();

        // 收集所有有效的缓存项
        foreach (var kvp in Cache)
        {
            if (TryGetCacheItem(kvp.Value, out var item) && !item.IsExpired)
            {
                items.Add(new KeyValuePair<string, ICacheItem>(kvp.Key, item));
            }
        }

        if (items.Count == 0)
        {
            return;
        }

        // 根据淘汰策略排序
        var itemsToEvict = Options.EvictionPolicy switch
        {
            CacheEvictionPolicy.Lru => items.OrderBy(x => x.Value.LastAccessed).Take(count),
            CacheEvictionPolicy.Lfu => items.OrderBy(x => x.Value.AccessCount).Take(count),
            CacheEvictionPolicy.Fifo => items.OrderBy(x => x.Value.CreatedAt).Take(count),
            _ => items.OrderBy(x => x.Value.LastAccessed).Take(count)
        };

        // 执行淘汰
        foreach (var item in itemsToEvict)
        {
            if (Cache.TryRemove(item.Key, out _))
            {
                KeyLocks.TryRemove(item.Key, out _);
                if (AsyncKeyLocks.TryRemove(item.Key, out var sem))
                {
                    sem.Dispose();
                }

                if (Options.EnableStatistics)
                {
                    Statistics.RecordEviction();
                }
                RaiseEvent(item.Key, CacheEventType.Evicted);
            }
        }
    }

    /// <summary>
    /// 将缓存键加入过期检查队列
    /// </summary>
    /// <param name="key">缓存键</param>
    private static void ScheduleExpirationCheck(string key)
    {
        ExpirationQueue.Enqueue(key);
    }

    /// <summary>
    /// 惰性清理过期的缓存项（从队列中批量处理）
    /// </summary>
    private static void LazyCleanupExpiredItems()
    {
        var processed = 0;
        while (processed < CleanupBatchSize && ExpirationQueue.TryDequeue(out var key))
        {
            processed++;

            // 检查缓存项是否过期
            if (Cache.TryGetValue(key, out var cacheObject) && TryGetCacheItem(cacheObject, out var item))
            {
                if (item.IsExpired)
                {
                    // 移除过期项
                    Cache.TryRemove(key, out _);
                    KeyLocks.TryRemove(key, out _);
                    if (AsyncKeyLocks.TryRemove(key, out var sem))
                    {
                        sem.Dispose();
                    }

                    if (Options.EnableStatistics)
                    {
                        Statistics.RecordExpired();
                    }
                    RaiseEvent(key, CacheEventType.Expired);
                }
                else
                {
                    // 如果还未过期，重新安排检查（根据下一个过期时间）
                    if (item.NextExpiration.HasValue)
                    {
                        ExpirationQueue.Enqueue(key);
                    }
                }
            }
            // 如果缓存项不存在，则无需处理
        }
    }

    /// <summary>
    /// 清理过期的缓存项（全量扫描，用于手动调用）
    /// </summary>
    private static void CleanupExpiredItems()
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
