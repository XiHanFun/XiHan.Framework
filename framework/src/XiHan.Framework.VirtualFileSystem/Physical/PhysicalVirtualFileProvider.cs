#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PhysicalVirtualFileProvider
// Guid:b8f1c680-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:18:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Utils.IO;
using XiHan.Framework.VirtualFileSystem.Core;

namespace XiHan.Framework.VirtualFileSystem.Physical;

/// <summary>
/// 物理文件虚拟提供者
/// </summary>
/// <remarks>
/// 提供对物理文件系统的访问，将物理文件系统适配为虚拟文件系统
/// </remarks>
public class PhysicalVirtualFileProvider : IVirtualFileProvider
{
    private readonly PhysicalFileProvider _physicalFileProvider;

    /// <summary>
    /// 初始化物理文件虚拟提供者
    /// </summary>
    /// <param name="root">根目录路径</param>
    /// <remarks>
    /// 如果根目录不存在，会自动创建
    /// </remarks>
    public PhysicalVirtualFileProvider(string root)
    {
        DirectoryHelper.CreateIfNotExists(root);
        _physicalFileProvider = new PhysicalFileProvider(root);
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>文件信息对象</returns>
    /// <remarks>
    /// 直接使用物理文件提供者获取文件信息
    /// </remarks>
    public IFileInfo GetFileInfo(string subpath)
    {
        return GetVirtualFile(subpath);
    }

    /// <summary>
    /// 获取虚拟文件
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>虚拟文件对象，如果文件不存在则返回 null</returns>
    /// <remarks>
    /// 将物理文件包装为虚拟文件对象
    /// </remarks>
    public IVirtualFile GetVirtualFile(string subpath)
    {
        var fileInfo = _physicalFileProvider.GetFileInfo(subpath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        return new PhysicalVirtualFile(fileInfo);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath">目录路径</param>
    /// <returns>目录内容对象</returns>
    /// <remarks>
    /// 直接使用物理文件提供者获取目录内容
    /// </remarks>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return _physicalFileProvider.GetDirectoryContents(subpath);
    }

    /// <summary>
    /// 监视文件变化
    /// </summary>
    /// <param name="filter">文件筛选器</param>
    /// <returns>变更令牌</returns>
    /// <remarks>
    /// 直接使用物理文件提供者的文件监视功能
    /// </remarks>
    public IChangeToken Watch(string filter)
    {
        return _physicalFileProvider.Watch(filter);
    }

    /// <summary>
    /// 添加文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    /// <remarks>
    /// 将文件流写入物理文件系统，如果目录不存在会自动创建
    /// </remarks>
    public void AddFile(string path, Stream stream)
    {
        var fullPath = Path.Combine(_physicalFileProvider.Root, path);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            DirectoryHelper.CreateIfNotExists(directory);
        }

        using var fileStream = File.Create(fullPath);
        stream.CopyTo(fileStream);
    }

    /// <summary>
    /// 移除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <remarks>
    /// 从物理文件系统中删除文件
    /// </remarks>
    public void RemoveFile(string path)
    {
        var fullPath = Path.Combine(_physicalFileProvider.Root, path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}

/// <summary>
/// 物理虚拟文件
/// </summary>
/// <remarks>
/// 将物理文件包装为虚拟文件，提供对物理文件的访问
/// </remarks>
internal class PhysicalVirtualFile : IVirtualFile
{
    private readonly IFileInfo _fileInfo;

    /// <summary>
    /// 初始化物理虚拟文件
    /// </summary>
    /// <param name="fileInfo">物理文件信息</param>
    public PhysicalVirtualFile(IFileInfo fileInfo)
    {
        _fileInfo = fileInfo;
        Name = fileInfo.Name;
        PhysicalPath = fileInfo.PhysicalPath;
        Length = fileInfo.Length;
        LastModified = fileInfo.LastModified;
        IsDirectory = fileInfo.IsDirectory;
        Exists = fileInfo.Exists;
    }

    /// <summary>
    /// 获取文件名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取文件大小（字节）
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// 获取最后修改时间
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    /// 获取是否是目录
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// 获取文件是否存在
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// 获取物理文件路径
    /// </summary>
    public string PhysicalPath { get; }

    /// <summary>
    /// 创建文件读取流
    /// </summary>
    /// <returns>文件流</returns>
    /// <remarks>
    /// 直接使用物理文件的流
    /// </remarks>
    public Stream CreateReadStream()
    {
        return _fileInfo.CreateReadStream();
    }

    /// <summary>
    /// 异步创建文件读取流
    /// </summary>
    /// <returns>文件流任务</returns>
    /// <remarks>
    /// 将同步流包装为异步任务
    /// </remarks>
    public Task<Stream> CreateReadStreamAsync()
    {
        return Task.FromResult(CreateReadStream());
    }
}
