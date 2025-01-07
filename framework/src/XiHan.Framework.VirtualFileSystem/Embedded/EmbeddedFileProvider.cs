#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmbeddedFileProvider
// Guid:e5f1c67d-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:15:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using XiHan.Framework.VirtualFileSystem.Core;

namespace XiHan.Framework.VirtualFileSystem.Embedded;

/// <summary>
/// 嵌入式文件提供者
/// </summary>
public class EmbeddedFileProvider : IVirtualFileProvider
{
    private readonly Assembly _assembly;
    private readonly string _baseNamespace;

    /// <summary>
    /// 初始化嵌入式文件提供者
    /// </summary>
    /// <param name="assembly">包含嵌入式资源的程序集</param>
    /// <param name="baseNamespace">基础命名空间</param>
    public EmbeddedFileProvider(Assembly assembly, string baseNamespace)
    {
        _assembly = assembly;
        _baseNamespace = baseNamespace;
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>文件信息对象</returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        return GetVirtualFile(subpath);
    }

    /// <summary>
    /// 获取虚拟文件
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>虚拟文件对象</returns>
    public IVirtualFile GetVirtualFile(string subpath)
    {
        var resourcePath = GetResourcePath(subpath);
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        var stream = _assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
        {
            return null;
        }

        return new EmbeddedVirtualFile(subpath, stream, resourcePath);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath">目录路径</param>
    /// <returns>目录内容对象</returns>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return new NotFoundDirectoryContents();
    }

    /// <summary>
    /// 监视文件变化
    /// </summary>
    /// <param name="filter">文件筛选器</param>
    /// <returns>变更令牌</returns>
    public IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    /// <summary>
    /// 添加文件（不支持）
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    /// <exception cref="NotSupportedException">嵌入式文件提供者不支持添加文件</exception>
    public void AddFile(string path, Stream stream)
    {
        throw new NotSupportedException("嵌入式文件提供者不支持添加文件");
    }

    /// <summary>
    /// 移除文件（不支持）
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <exception cref="NotSupportedException">嵌入式文件提供者不支持移除文件</exception>
    public void RemoveFile(string path)
    {
        throw new NotSupportedException("嵌入式文件提供者不支持移除文件");
    }

    private string GetResourcePath(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return null;
        }

        var resourcePath = subpath.Replace('/', '.');
        if (!string.IsNullOrEmpty(_baseNamespace))
        {
            resourcePath = _baseNamespace + "." + resourcePath;
        }

        return resourcePath;
    }
}

/// <summary>
/// 嵌入式虚拟文件
/// </summary>
internal class EmbeddedVirtualFile : IVirtualFile
{
    private readonly Stream _stream;

    /// <summary>
    /// 初始化嵌入式虚拟文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    /// <param name="resourcePath">资源路径</param>
    public EmbeddedVirtualFile(string path, Stream stream, string resourcePath)
    {
        _stream = stream;
        Name = Path.GetFileName(path);
        PhysicalPath = null;
        Length = stream.Length;
        LastModified = DateTimeOffset.UtcNow;
        IsDirectory = false;
        Exists = true;
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
    public Stream CreateReadStream()
    {
        return _stream;
    }

    /// <summary>
    /// 异步创建文件读取流
    /// </summary>
    /// <returns>文件流任务</returns>
    public Task<Stream> CreateReadStreamAsync()
    {
        return Task.FromResult(_stream);
    }
}
