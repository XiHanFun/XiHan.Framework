// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Collections.Concurrent;
using XiHan.Framework.VirtualFileSystem.Models;

namespace XiHan.Framework.VirtualFileSystem.Services;

/// <summary>
/// 文件版本控制服务
/// </summary>
public class FileVersioningService : IFileVersioningService
{
    private readonly ConcurrentDictionary<string, Stack<FileVersion>> _fileVersions = new();

    /// <summary>
    /// 快照
    /// </summary>
    /// <param name="file"></param>
    public void Snapshot(IFileInfo file)
    {
        if (file.PhysicalPath is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        var versions = _fileVersions.GetOrAdd(file.PhysicalPath, _ => new Stack<FileVersion>());
        versions.Push(new FileVersion(file));
    }

    /// <summary>
    /// 回滚
    /// </summary>
    /// <param name="path"></param>
    /// <param name="steps"></param>
    /// <returns></returns>
    public bool Rollback(string path, int steps = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (steps <= 0)
        {
            return false;
        }

        if (!_fileVersions.TryGetValue(path, out var versions))
        {
            return false;
        }

        FileVersion? targetVersion = null;
        while (steps-- > 0 && versions.Count > 0)
        {
            targetVersion = versions.Pop();
        }

        if (targetVersion is null)
        {
            return false;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        File.WriteAllBytes(path, targetVersion.Content);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        return true;
    }
}
