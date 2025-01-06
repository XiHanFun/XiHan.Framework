#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOperations
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:32:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统操作类
/// </summary>
public class VirtualFileSystemOperations : IVirtualFileSystemOperations
{
    private readonly IVirtualFileSystemManager _virtualFileSystemManager;

    public VirtualFileSystemOperations(IVirtualFileSystemManager virtualFileSystemManager)
    {
        _virtualFileSystemManager = virtualFileSystemManager;
    }

    /// <summary>
    /// 文件是否存在
    /// </summary>
    public bool FileExists(string filePath)
    {
        return GetFileInfo(filePath).Exists;
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    public IFileInfo GetFileInfo(string filePath)
    {
        return _virtualFileSystemManager.GetFileProvider().GetFileInfo(filePath);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    public IDirectoryContents GetDirectoryContents(string path)
    {
        return _virtualFileSystemManager.GetFileProvider().GetDirectoryContents(path);
    }

    /// <summary>
    /// 读取文件内容
    /// </summary>
    public async Task<string> ReadFileAsync(string filePath)
    {
        var fileInfo = GetFileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 获取所有文件
    /// </summary>
    public IEnumerable<IFileInfo> GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var directory = GetDirectoryContents(path);
        if (!directory.Exists)
        {
            yield break;
        }

        foreach (var file in directory)
        {
            if (!file.IsDirectory && IsFileMatchPattern(file.Name, searchPattern))
            {
                yield return file;
            }

            if (file.IsDirectory && searchOption == SearchOption.AllDirectories)
            {
                var subFiles = GetFiles(Path.Combine(path, file.Name), searchPattern, searchOption);
                foreach (var subFile in subFiles)
                {
                    yield return subFile;
                }
            }
        }
    }

    private bool IsFileMatchPattern(string fileName, string pattern)
    {
        return pattern == "*" || fileName.EndsWith(pattern.TrimStart('*'));
    }
}
