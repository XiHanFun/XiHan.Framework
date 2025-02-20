#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOperations
// Guid:a38f624a-a55d-42b5-9efd-6673f14cf9dd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:32:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件系统操作类
/// </summary>
public class VirtualFileSystemOperations : IVirtualFileSystemOperations
{
    private readonly IVirtualFileSystemManager _virtualFileSystemManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="virtualFileSystemManager"></param>
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

    /// <summary>
    /// 文件是否匹配模式
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="pattern"></param>
    /// <returns></returns>
    private bool IsFileMatchPattern(string fileName, string pattern)
    {
        return pattern == "*" || fileName.EndsWith(pattern.TrimStart('*'));
    }
}
