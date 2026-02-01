#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileVersioningService
// Guid:5a8b6653-18cf-4538-bfb5-dc3a381cdbbc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 08:06:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        if (!_fileVersions.TryGetValue(path, out var versions))
        {
            return false;
        }

        while (steps-- > 0 && versions.Count > 0)
        {
            versions.Pop();
        }
        return true;
    }
}
