using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.FileSystem.Services;

/// <summary>
/// 文件版本控制服务
/// </summary>
public interface IFileVersioningService
{
    /// <summary>
    /// 回滚
    /// </summary>
    /// <param name="path"></param>
    /// <param name="steps"></param>
    /// <returns></returns>
    bool Rollback(string path, int steps = 1);

    /// <summary>
    /// 快照
    /// </summary>
    /// <param name="file"></param>
    void Snapshot(IFileInfo file);
}
