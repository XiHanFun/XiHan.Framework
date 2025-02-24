namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件缓存服务
/// </summary>
public interface IFileCacheService
{
    /// <summary>
    /// 获取或添加缓存
    /// </summary>
    /// <param name="path"></param>
    /// <param name="factory"></param>
    /// <param name="expiration"></param>
    /// <returns></returns>
    Task<byte[]> GetOrAddAsync(string path, Func<Task<byte[]>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// 清除缓存
    /// </summary>
    /// <param name="path"></param>
    void Remove(string path);

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    void Clear();
}
