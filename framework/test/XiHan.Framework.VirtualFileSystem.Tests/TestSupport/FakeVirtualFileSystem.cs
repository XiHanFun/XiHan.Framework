// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 手写的虚拟文件系统替身
/// </summary>
/// <remarks>
/// 只用于验证 TryAddSingleton 的「已注册则不覆盖」语义，所有成员返回空实现。
/// </remarks>
internal sealed class FakeVirtualFileSystem : IVirtualFileSystem
{
    /// <summary>
    /// 文件变化事件
    /// </summary>
    public event EventHandler<FileChangedEventArgs> OnFileChanged
    {
        add { }
        remove { }
    }

    /// <summary>
    /// 判断目录是否存在
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>始终为 false</returns>
    public bool DirectoryExists(string virtualPath)
    {
        return false;
    }

    /// <summary>
    /// 枚举文件
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <param name="searchPattern">搜索模式</param>
    /// <param name="recursive">是否递归</param>
    /// <returns>始终为空集合</returns>
    public IReadOnlyList<string> EnumerateFiles(string virtualPath, string searchPattern = "*", bool recursive = true)
    {
        return [];
    }

    /// <summary>
    /// 判断文件是否存在
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>始终为 false</returns>
    public bool FileExists(string virtualPath)
    {
        return false;
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>不存在的目录内容</returns>
    public IDirectoryContents GetDirectoryContents(string virtualPath)
    {
        return new FakeDirectoryContents(false);
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>未找到的文件信息</returns>
    public IFileInfo GetFile(string virtualPath)
    {
        return new NotFoundFileInfo(virtualPath);
    }

    /// <summary>
    /// 挂载文件提供程序
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <param name="priority">优先级</param>
    public void Mount(IFileProvider provider, int priority = 0)
    {
    }

    /// <summary>
    /// 卸载文件提供程序
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <returns>始终为 false</returns>
    public bool Unmount(IFileProvider provider)
    {
        return false;
    }

    /// <summary>
    /// 监控文件变化
    /// </summary>
    /// <param name="filter">过滤条件</param>
    /// <returns>变更令牌</returns>
    public IChangeToken Watch(string filter)
    {
        return new FakeChangeToken();
    }
}
