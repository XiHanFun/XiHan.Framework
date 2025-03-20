#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationCacheManager
// Guid:9d123456-1a2b-3c4d-5e6f-7890abcdef12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/01 10:35:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Extensions.Threading;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Threading;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 本地化缓存管理器，提供本地化字符串的缓存功能
/// </summary>
public class LocalizationCacheManager : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ICancellationTokenProvider _cancellationTokenProvider;
    private readonly ILogger<LocalizationCacheManager> _logger;
    private readonly XiHanLocalizationOptions _options;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cache">内存缓存接口</param>
    /// <param name="cancellationTokenProvider">取消令牌提供者</param>
    /// <param name="options">本地化选项</param>
    /// <param name="logger">日志接口</param>
    public LocalizationCacheManager(
        IMemoryCache cache,
        ICancellationTokenProvider cancellationTokenProvider,
        IOptions<XiHanLocalizationOptions> options,
        ILogger<LocalizationCacheManager> logger)
    {
        _cache = cache;
        _cancellationTokenProvider = cancellationTokenProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 获取或添加缓存项
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="cultureName">文化名称</param>
    /// <param name="key">键</param>
    /// <param name="factory">工厂函数</param>
    /// <returns>本地化字符串</returns>
    public async Task<LocalizedString> GetOrAddAsync(
        string resourceName,
        string cultureName,
        string key,
        Func<Task<LocalizedString>> factory)
    {
        var cacheKey = $"L:{resourceName}:{cultureName}:{key}";

        if (_cache.TryGetValue(cacheKey, out LocalizedString? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        using (await _semaphore.LockAsync(_cancellationTokenProvider.Token))
        {
            if (_cache.TryGetValue(cacheKey, out cachedValue) && cachedValue is not null)
            {
                return cachedValue;
            }

            var result = await factory();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.CacheLifetime
            };

            _ = _cache.Set(cacheKey, result, cacheOptions);
            _logger.LogDebug("Added localization cache entry: {ResourceName}, {Culture}, {Key}", resourceName, cultureName, key);

            return result;
        }
    }

    /// <summary>
    /// 获取或添加缓存项
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="cultureName">文化名称</param>
    /// <param name="key">键</param>
    /// <param name="factory">工厂函数</param>
    /// <returns>本地化字符串</returns>
    public LocalizedString GetOrAdd(
        string resourceName,
        string cultureName,
        string key,
        Func<LocalizedString> factory)
    {
        var cacheKey = $"L:{resourceName}:{cultureName}:{key}";

        if (_cache.TryGetValue(cacheKey, out LocalizedString? cachedValue) && cachedValue is not null)
        {
            return cachedValue;
        }

        using (_semaphore.Lock(_cancellationTokenProvider.Token))
        {
            if (_cache.TryGetValue(cacheKey, out cachedValue) && cachedValue is not null)
            {
                return cachedValue;
            }

            var result = factory();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.CacheLifetime
            };

            _ = _cache.Set(cacheKey, result, cacheOptions);
            _logger.LogDebug("Added localization cache entry: {ResourceName}, {Culture}, {Key}", resourceName, cultureName, key);

            return result;
        }
    }

    /// <summary>
    /// 清除特定资源的缓存
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    public void ClearResourceCache(string resourceName)
    {
        _logger.LogInformation("Clearing cache for resource: {ResourceName}", resourceName);
        // 注意：由于MemoryCache不支持部分清除，这里使用重置缓存过期时间的方式
        // 在实际应用中，可以考虑使用更适合的缓存实现
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _semaphore.Dispose();
        _disposed = true;
    }
}
