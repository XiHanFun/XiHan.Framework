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
    private static readonly ConcurrentDictionary<string, object> Cache = new();
    private static readonly Timer CleanupTimer;

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

        if (Cache.TryGetValue(key, out var cacheObject) && cacheObject is CacheItem<T> cacheItem)
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
    /// <param name="expireSeconds">过期秒数</param>
    public static void Set<T>(string key, T value, int expireSeconds = 3600)
    {
        Set(key, value, DateTimeOffset.Now.AddSeconds(expireSeconds));
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
        Set(key, value, DateTimeOffset.Now.Add(expireSpan));
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

        if (Cache.TryGetValue(key, out var cacheObject))
        {
            // 检查是否是泛型缓存项且未过期
            if (cacheObject.GetType().IsGenericType &&
                cacheObject.GetType().GetGenericTypeDefinition() == typeof(CacheItem<>))
            {
                // 通过反射获取 IsExpired 属性
                var isExpiredProperty = cacheObject.GetType().GetProperty("IsExpired");
                if (isExpiredProperty?.GetValue(cacheObject) is bool isExpired)
                {
                    if (isExpired)
                    {
                        Cache.TryRemove(key, out _);
                        return false;
                    }
                    return true;
                }
            }
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
        var value = Get<T>(key);
        if (value != null)
        {
            return value;
        }

        value = factory();
        Set(key, value, expireTime);
        return value;
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
        return GetOrAdd(key, factory, DateTimeOffset.Now.AddSeconds(expireSeconds));
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
        try
        {
            value = Get<T>(key);
            return value != null;
        }
        catch
        {
            value = default;
            return false;
        }
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
    /// <param name="expireSeconds">过期秒数</param>
    public static void SetAll<T>(IEnumerable<KeyValuePair<string, T>> items, int expireSeconds = 3600)
    {
        var expireTime = DateTimeOffset.Now.AddSeconds(expireSeconds);
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
            // 检查是否是泛型缓存项且已过期
            if (kvp.Value.GetType().IsGenericType &&
                kvp.Value.GetType().GetGenericTypeDefinition() == typeof(CacheItem<>))
            {
                var isExpiredProperty = kvp.Value.GetType().GetProperty("IsExpired");
                if (isExpiredProperty?.GetValue(kvp.Value) is bool isExpired && isExpired)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
        }

        foreach (var key in expiredKeys)
        {
            Cache.TryRemove(key, out _);
        }
    }

    #endregion
}
