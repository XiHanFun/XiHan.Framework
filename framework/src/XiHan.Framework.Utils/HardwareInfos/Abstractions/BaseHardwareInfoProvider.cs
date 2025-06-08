#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BaseHardwareInfoProvider
// Guid:B2C3D4E5-F6A7-8901-B2C3-D4E5F6A78901
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2025-01-01 上午 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.HardwareInfos.Abstractions;

/// <summary>
/// 硬件信息提供者基础抽象类
/// </summary>
/// <typeparam name="T">硬件信息类型</typeparam>
public abstract class BaseHardwareInfoProvider<T> : IHardwareInfoProvider<T> where T : class, new()
{
    private static readonly ConcurrentDictionary<Type, (T info, DateTime cacheTime)> _cache = new();
    private static readonly TimeSpan _defaultCacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 缓存过期时间
    /// </summary>
    protected virtual TimeSpan CacheExpiry => _defaultCacheExpiry;

    /// <summary>
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 清除指定类型的缓存
    /// </summary>
    /// <typeparam name="TInfo">硬件信息类型</typeparam>
    public static void ClearCache<TInfo>() where TInfo : class
    {
        _cache.TryRemove(typeof(TInfo), out _);
    }

    /// <summary>
    /// 获取硬件信息
    /// </summary>
    /// <returns>硬件信息</returns>
    public T GetInfo()
    {
        try
        {
            return GetInfoCore();
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"获取{typeof(T).Name}信息出错：{ex.Message}");
            return CreateErrorInfo(ex.Message);
        }
    }

    /// <summary>
    /// 异步获取硬件信息
    /// </summary>
    /// <returns>硬件信息</returns>
    public async Task<T> GetInfoAsync()
    {
        try
        {
            return await GetInfoCoreAsync();
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"异步获取{typeof(T).Name}信息出错：{ex.Message}");
            return CreateErrorInfo(ex.Message);
        }
    }

    /// <summary>
    /// 获取缓存的硬件信息
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新缓存</param>
    /// <returns>硬件信息</returns>
    public T GetCachedInfo(bool forceRefresh = false)
    {
        var type = typeof(T);

        if (!forceRefresh && _cache.TryGetValue(type, out var cached))
        {
            if (DateTime.Now - cached.cacheTime < CacheExpiry)
            {
                return cached.info;
            }
        }

        var info = GetInfo();
        _cache.AddOrUpdate(type, (info, DateTime.Now), (_, _) => (info, DateTime.Now));
        return info;
    }

    /// <summary>
    /// 异步获取缓存的硬件信息
    /// </summary>
    /// <param name="forceRefresh">是否强制刷新缓存</param>
    /// <returns>硬件信息</returns>
    public async Task<T> GetCachedInfoAsync(bool forceRefresh = false)
    {
        var type = typeof(T);

        if (!forceRefresh && _cache.TryGetValue(type, out var cached))
        {
            if (DateTime.Now - cached.cacheTime < CacheExpiry)
            {
                return cached.info;
            }
        }

        var info = await GetInfoAsync();
        _cache.AddOrUpdate(type, (info, DateTime.Now), (_, _) => (info, DateTime.Now));
        return info;
    }

    /// <summary>
    /// 获取硬件信息的核心实现
    /// </summary>
    /// <returns>硬件信息</returns>
    protected abstract T GetInfoCore();

    /// <summary>
    /// 异步获取硬件信息的核心实现
    /// </summary>
    /// <returns>硬件信息</returns>
    protected virtual async Task<T> GetInfoCoreAsync()
    {
        return await Task.Run(GetInfoCore);
    }

    /// <summary>
    /// 创建错误信息对象
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>错误信息对象</returns>
    protected virtual T CreateErrorInfo(string errorMessage)
    {
        var info = new T();

        // 使用反射设置基础属性（如果类型实现了IHardwareInfo）
        if (info is IHardwareInfo)
        {
            var timestampProp = typeof(T).GetProperty(nameof(IHardwareInfo.Timestamp));
            var isAvailableProp = typeof(T).GetProperty(nameof(IHardwareInfo.IsAvailable));
            var errorMessageProp = typeof(T).GetProperty(nameof(IHardwareInfo.ErrorMessage));

            timestampProp?.SetValue(info, DateTime.Now);
            isAvailableProp?.SetValue(info, false);
            errorMessageProp?.SetValue(info, errorMessage);
        }

        return info;
    }
}
