using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件缓存服务
/// </summary>
public class FileCacheService : IFileCacheService
{
    private readonly MemoryCache _cache;
    private readonly ILogger<FileCacheService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    public FileCacheService(ILogger<FileCacheService> logger)
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            // 100MB
            SizeLimit = 1024 * 1024 * 100
        });
        _logger = logger;
    }

    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <param name="path"></param>
    /// <param name="factory"></param>
    /// <param name="expiration"></param>
    /// <returns></returns>
    public async Task<byte[]> GetOrAddAsync(string path, Func<Task<byte[]>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(path, out var cachedContent) && cachedContent is byte[] content)
        {
            return content;
        }

        content = await factory();
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(content.Length)
            .SetPriority(CacheItemPriority.Normal);

        if (expiration.HasValue)
        {
            _ = cacheEntryOptions.SetAbsoluteExpiration(expiration.Value);
        }

        _ = _cache.Set(path, content, cacheEntryOptions);
        return content;
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <param name="path"></param>
    public void Remove(string path)
    {
        _cache.Remove(path);
        _logger.LogInformation("已清除文件缓存: {Path}", path);
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }
}
