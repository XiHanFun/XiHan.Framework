#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFileProvider
// Guid:c3f1c67b-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:13:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件提供者接口
/// </summary>
/// <remarks>
/// 继承自 Microsoft.Extensions.FileProviders.IFileProvider，
/// 提供了虚拟文件系统的核心功能接口
/// </remarks>
public interface IVirtualFileProvider : IFileProvider
{
    /// <summary>
    /// 获取虚拟文件
    /// </summary>
    /// <param name="subpath">文件的相对路径</param>
    /// <returns>虚拟文件对象，如果文件不存在则返回 null</returns>
    /// <remarks>
    /// 通过相对路径获取虚拟文件，路径使用正斜杠(/)作为分隔符
    /// </remarks>
    IVirtualFile GetVirtualFile(string subpath);

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath">目录的相对路径</param>
    /// <returns>目录内容对象</returns>
    /// <remarks>
    /// 获取指定目录下的所有文件和子目录
    /// </remarks>
    new IDirectoryContents GetDirectoryContents(string subpath);

    /// <summary>
    /// 监视文件变化
    /// </summary>
    /// <param name="filter">文件筛选器</param>
    /// <returns>变更令牌</returns>
    /// <remarks>
    /// 用于监视文件或目录的变化，支持通配符
    /// 例如: "*.json" 监视所有 JSON 文件
    /// </remarks>
    new IChangeToken Watch(string filter);

    /// <summary>
    /// 添加文件到虚拟文件系统
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    /// <remarks>
    /// 将文件流添加到虚拟文件系统中，如果文件已存在则覆盖
    /// </remarks>
    void AddFile(string path, Stream stream);

    /// <summary>
    /// 从虚拟文件系统移除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <remarks>
    /// 从虚拟文件系统中移除指定路径的文件
    /// </remarks>
    void RemoveFile(string path);
}
