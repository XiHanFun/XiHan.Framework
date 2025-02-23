using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// �ļ��������
/// </summary>
public class FileCacheService
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultExpiration;

    /// <summary>
    /// ���캯��
    /// </summary>
    /// <param name="memoryCache"></param>
    /// <param name="defaultExpiration"></param>
    public FileCacheService(IMemoryCache memoryCache, TimeSpan defaultExpiration)
    {
        _cache = memoryCache;
        _defaultExpiration = defaultExpiration;
    }

    /// <summary>
    /// ��ȡ�򻺴�
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
