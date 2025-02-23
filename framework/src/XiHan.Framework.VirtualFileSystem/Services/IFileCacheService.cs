using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件缓存服务
/// </summary>
public interface IFileCacheService
{
    /// <summary>
    /// 获取或缓存
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    IFileInfo? GetOrCache(string key, Func<IFileInfo> factory);

    /// <summary>
    /// 移除
    /// </summary>
    /// <param name="key"></param>
    void Remove(string key);
}
