#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualMemoryFileProvider
// Guid:15d4f7b6-b779-4cf0-b2dc-f8f0b56bdec0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 7:23:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace XiHan.Framework.VirtualFileSystem.Providers.Memory;

/// <summary>
/// 内存文件提供程序
/// </summary>
public class VirtualMemoryFileProvider : IFileProvider
{
    private readonly ConcurrentDictionary<string, MemoryFile> _files = new();

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        return _files.TryGetValue(subpath, out var file)
            ? new MemoryFileInfo(file)
            : new NotFoundFileInfo(subpath);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var files = _files.Where(f => f.Key.StartsWith(subpath))
                          .Select(f => new MemoryFileInfo(f.Value));
        return new EnumerableDirectoryContents(files);
    }

    /// <summary>
    /// 监视
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public IChangeToken Watch(string filter)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 更新文件
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    public void UpdateFile(string path, byte[] content)
    {
        _ = _files.AddOrUpdate(path,
            new MemoryFile(path, content),
            (_, existing) => existing.UpdateContent(content));
    }
}
