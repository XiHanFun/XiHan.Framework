#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheManager
// Guid:d8c73bf7-5e91-4a68-af0b-7f23d6e8a23a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 7:44:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存管理器
/// </summary>
public class CacheManager
{
    /// <summary>
    /// 默认缓存名称
    /// </summary>
    public const string DefaultCacheName = "Default";

    private static readonly Lazy<CacheManager> LazyInstance = new(() => new CacheManager());

    private readonly Dictionary<string, MemoryCache> _caches = [];

    private readonly Lock _syncLock = new();

    private readonly MemoryCache _defaultCache;

    /// <summary>
    /// 私有构造函数
    /// </summary>
    private CacheManager()
    {
        _defaultCache = new MemoryCache();
        _caches[DefaultCacheName] = _defaultCache;
    }

    /// <summary>
    /// 单例实例
    /// </summary>
    public static CacheManager Instance => LazyInstance.Value;

    /// <summary>
    /// 默认缓存实例
    /// </summary>
    public static MemoryCache InstanceDefaultCache => LazyInstance.Value.DefaultCache;

    /// <summary>
    /// 默认缓存实例
    /// </summary>
    public MemoryCache DefaultCache => _defaultCache;

    /// <summary>
    /// 获取命名缓存实例，如果不存在则创建
    /// </summary>
    /// <param name="name">缓存名称</param>
    /// <param name="enableCleanup">是否启用自动清理</param>
    /// <param name="cleanupIntervalMinutes">清理间隔(分钟)</param>
    /// <returns>内存缓存实例</returns>
    public MemoryCache GetOrAdd(string name, bool enableCleanup = true, int cleanupIntervalMinutes = 10)
    {
        if (string.IsNullOrEmpty(name))
        {
            return _defaultCache;
        }

        lock (_syncLock)
        {
            if (_caches.TryGetValue(name, out var cache))
            {
                return cache;
            }

            cache = new MemoryCache(enableCleanup, cleanupIntervalMinutes);
            _caches[name] = cache;
            return cache;
        }
    }

    /// <summary>
    /// 移除命名缓存实例
    /// </summary>
    /// <param name="name">缓存名称</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveCache(string name)
    {
        // 不允许移除默认缓存
        if (string.IsNullOrEmpty(name) || name == DefaultCacheName)
        {
            return false;
        }

        lock (_syncLock)
        {
            return _caches.Remove(name);
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void ClearAllCaches()
    {
        lock (_syncLock)
        {
            foreach (var cache in _caches.Values)
            {
                cache.Clear();
            }
        }
    }

    /// <summary>
    /// 获取所有缓存名称
    /// </summary>
    /// <returns>缓存名称列表</returns>
    public IEnumerable<string> GetCacheNames()
    {
        lock (_syncLock)
        {
            return [.. _caches.Keys];
        }
    }

    /// <summary>
    /// 获取缓存实例数量
    /// </summary>
    /// <returns>缓存实例数量</returns>
    public int GetCacheCount()
    {
        lock (_syncLock)
        {
            return _caches.Count;
        }
    }

    /// <summary>
    /// 检查缓存是否存在
    /// </summary>
    /// <param name="name">缓存名称</param>
    /// <returns>是否存在</returns>
    public bool CacheExists(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        lock (_syncLock)
        {
            return _caches.ContainsKey(name);
        }
    }
}
