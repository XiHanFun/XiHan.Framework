using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件缓存服务
/// </summary>
public class FileCacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultExpiration;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="memoryCache"></param>
    /// <param name="defaultExpiration"></param>
    public FileCacheService(IMemoryCache memoryCache, TimeSpan defaultExpiration)
    {
        _cache = memoryCache;
        _defaultExpiration = defaultExpiration;
    }

    /// <summary>
    /// 获取或缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    public IFileInfo? GetOrCache(string key, Func<IFileInfo> factory)
    {
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _defaultExpiration;
            return factory();
        });
    }
}
