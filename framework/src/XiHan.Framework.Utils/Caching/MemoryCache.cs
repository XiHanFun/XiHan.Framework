#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryCache
// Guid:f871e4a5-6b7c-4fa9-9c6e-13d4fd9b76f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 7:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 内存缓存
/// </summary>
public class MemoryCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Timer? _cleanupTimer;
    private readonly bool _enableCleanup;

    /// <summary>
    /// 内存缓存构造函数
    /// </summary>
    /// <param name="enableCleanup">是否启用自动清理</param>
    /// <param name="cleanupIntervalMinutes">清理间隔(分钟)</param>
    public MemoryCache(bool enableCleanup = true, int cleanupIntervalMinutes = 10)
    {
        _enableCleanup = enableCleanup;
        var cleanupInterval = TimeSpan.FromMinutes(cleanupIntervalMinutes);

        if (_enableCleanup)
        {
            _cleanupTimer = new Timer(CleanupCallback, null, cleanupInterval, cleanupInterval);
        }
    }

    /// <summary>
    /// 在析构函数中停止清理定时器
    /// </summary>
    ~MemoryCache()
    {
        if (_enableCleanup)
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// 设置缓存项
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="absoluteExpiration">绝对过期时间</param>
    /// <returns>是否设置成功</returns>
    public bool Set<T>(string key, T value, DateTime? absoluteExpiration = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var cacheItem = new CacheItem<T>(value, absoluteExpiration);
        _cache.AddOrUpdate(key, cacheItem, (_, _) => cacheItem);
        return true;
    }

    /// <summary>
    /// 设置缓存项，使用相对过期时间
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="slidingExpiration">相对过期时间</param>
    /// <returns>是否设置成功</returns>
    public bool Set<T>(string key, T value, TimeSpan slidingExpiration)
    {
        return Set(key, value, DateTime.Now.Add(slidingExpiration));
    }

    /// <summary>
    /// 获取缓存项
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <returns>是否获取成功</returns>
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;

        if (string.IsNullOrEmpty(key) || !_cache.TryGetValue(key, out var obj))
        {
            return false;
        }

        if (obj is not CacheItem<T> cacheItem)
        {
            return false;
        }

        if (cacheItem.IsExpired)
        {
            _ = _cache.TryRemove(key, out _);
            return false;
        }

        value = cacheItem.Value;
        return true;
    }

    /// <summary>
    /// 获取缓存项，如果不存在则创建
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="valueFactory">值工厂</param>
    /// <param name="absoluteExpiration">绝对过期时间</param>
    /// <returns>缓存值</returns>
    public T GetOrAdd<T>(string key, Func<T> valueFactory, DateTime? absoluteExpiration = null)
    {
        if (TryGet<T>(key, out var value) && value != null)
        {
            return value;
        }

        value = valueFactory();
        _ = Set(key, value, absoluteExpiration);
        return value;
    }

    /// <summary>
    /// 获取缓存项，如果不存在则创建(使用相对过期时间)
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="valueFactory">值工厂</param>
    /// <param name="slidingExpiration">相对过期时间</param>
    /// <returns>缓存值</returns>
    public T GetOrAdd<T>(string key, Func<T> valueFactory, TimeSpan slidingExpiration)
    {
        return GetOrAdd(key, valueFactory, DateTime.Now.Add(slidingExpiration));
    }

    /// <summary>
    /// 获取缓存项，如果不存在则使用异步方式创建
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="valueFactory">异步值工厂</param>
    /// <param name="absoluteExpiration">绝对过期时间</param>
    /// <returns>缓存值</returns>
    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, DateTime? absoluteExpiration = null)
    {
        if (TryGet<T>(key, out var value) && value != null)
        {
            return value;
        }

        value = await valueFactory();
        _ = Set(key, value, absoluteExpiration);
        return value;
    }

    /// <summary>
    /// 获取缓存项，如果不存在则使用异步方式创建(使用相对过期时间)
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="valueFactory">异步值工厂</param>
    /// <param name="slidingExpiration">相对过期时间</param>
    /// <returns>缓存值</returns>
    public Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan slidingExpiration)
    {
        return GetOrAddAsync(key, valueFactory, DateTime.Now.Add(slidingExpiration));
    }

    /// <summary>
    /// 移除缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否移除成功</returns>
    public bool Remove(string key)
    {
        return !string.IsNullOrEmpty(key) && _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// 清空所有缓存项
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取缓存项数量
    /// </summary>
    /// <returns>缓存项数量</returns>
    public int Count()
    {
        return _cache.Count;
    }

    /// <summary>
    /// 检查缓存项是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否存在</returns>
    public bool Contains(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (!_cache.TryGetValue(key, out _))
        {
            return false;
        }

        if (!IsExpired(key))
        {
            return true;
        }

        _ = _cache.TryRemove(key, out _);
        return false;
    }

    /// <summary>
    /// 对所有缓存项执行操作
    /// </summary>
    /// <typeparam name="T">缓存项类型</typeparam>
    /// <param name="action">执行的操作</param>
    public void ForEach<T>(Action<string, T> action)
    {
        foreach (var item in _cache)
        {
            if (item.Value is CacheItem<T> { IsExpired: false } cacheItem)
            {
                action(item.Key, cacheItem.Value);
            }
        }
    }

    /// <summary>
    /// 清理过期缓存项
    /// </summary>
    /// <param name="state">状态对象</param>
    private void CleanupCallback(object? state)
    {
        var keysToRemove = new List<string>();

        foreach (var key in _cache.Keys)
        {
            if (IsExpired(key))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _ = _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// 检查键是否过期
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否过期</returns>
    private bool IsExpired(string key)
    {
        if (!_cache.TryGetValue(key, out var obj))
        {
            return true;
        }

        var type = obj.GetType();

        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(CacheItem<>))
        {
            return false;
        }

        var isExpiredProperty = type.GetProperty("IsExpired");
        return isExpiredProperty != null && (bool)isExpiredProperty.GetValue(obj)!;
    }
}
