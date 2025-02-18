#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEmbeddedFileProvider
// Guid:c3f09c89-702d-4851-8765-5f8444b1d707
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:15:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.VirtualFileSystem.Core;

namespace XiHan.Framework.VirtualFileSystem.Embedded;

/// <summary>
/// 嵌入式文件提供者
/// </summary>
/// <remarks>
/// 提供对嵌入式资源的访问，将嵌入式资源适配为虚拟文件系统
/// </remarks>
public class XiHanEmbeddedFileProvider : IVirtualFileProvider
{
    private readonly Assembly _assembly;
    private readonly string _baseNamespace;

    /// <summary>
    /// 初始化嵌入式文件提供者
    /// </summary>
    /// <param name="assembly">包含嵌入式资源的程序集</param>
    /// <param name="baseNamespace">基础命名空间</param>
    public XiHanEmbeddedFileProvider(Assembly assembly, string baseNamespace)
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
        var stream = _assembly.GetManifestResourceStream(resourcePath);
        return stream == null
            ? throw new XiHanException("未找到嵌入式资源：" + resourcePath)
            : (IVirtualFile)new EmbeddedVirtualFile(stream, subpath, resourcePath);
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

    /// <summary>
    /// 获取资源路径
    /// </summary>
    /// <param name="subpath">子路径</param>
    /// <returns></returns>
    private string GetResourcePath(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            throw new ArgumentNullException(nameof(subpath));
        }

        var resourcePath = subpath.Replace('/', '.');
        if (!string.IsNullOrEmpty(_baseNamespace))
        {
            resourcePath = _baseNamespace + "." + resourcePath;
        }

        return resourcePath;
    }
}
