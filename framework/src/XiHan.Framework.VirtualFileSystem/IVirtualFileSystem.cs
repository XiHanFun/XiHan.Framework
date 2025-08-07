#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFileSystem
// Guid:ba9402f2-6e43-43fb-a2d1-c493e3891607
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 5:36:29
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 统一虚拟文件系统接口
/// </summary>
public interface IVirtualFileSystem
{
    /// <summary>
    /// 文件变化事件
    /// </summary>
    event EventHandler<FileChangedEventArgs> OnFileChanged;

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="virtualPath">虚拟路径(支持~/embedded://等格式)</param>
    /// <returns>文件信息对象</returns>
    /// <remarks>当文件不存在时返回NotFoundFileInfo实例</remarks>
    IFileInfo GetFile(string virtualPath);

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="virtualPath">虚拟目录路径</param>
    /// <returns>目录内容集合</returns>
    /// <remarks>当目录不存在时返回NotFoundDirectoryContents实例</remarks>
    IDirectoryContents GetDirectoryContents(string virtualPath);

    /// <summary>
    /// 监控文件变化
    /// </summary>
    /// <param name="filter">监控过滤条件(支持通配符)</param>
    /// <returns>变化观察令牌</returns>
    /// <example>Watch("**/*.json") 监控所有JSON文件变化</example>
    IChangeToken Watch(string filter);

    /// <summary>
    /// 挂载文件提供程序
    /// </summary>
    /// <param name="provider">要挂载的文件提供程序</param>
    /// <param name="priority">优先级(数值越大优先级越高)</param>
    /// <exception cref="ArgumentNullException">当provider为null时抛出</exception>
    void Mount(IFileProvider provider, int priority = 0);

    /// <summary>
    /// 卸载文件提供程序
    /// </summary>
    /// <param name="provider">要卸载的文件提供程序</param>
    /// <returns>是否成功卸载</returns>
    bool Unmount(IFileProvider provider);
}
