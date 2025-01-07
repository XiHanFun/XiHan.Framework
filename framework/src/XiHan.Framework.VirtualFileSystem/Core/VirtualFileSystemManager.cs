#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemManager
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:22:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.VirtualFileSystem.Memory;
using XiHan.Framework.VirtualFileSystem.Physical;
using EmbeddedFileProvider = XiHan.Framework.VirtualFileSystem.Embedded.EmbeddedFileProvider;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件系统管理器
/// </summary>
public class VirtualFileSystemManager : IVirtualFileSystemManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VirtualFileSystemOptions _options;
    private readonly List<IVirtualFileProvider> _fileProviders;
    private CompositeFileProvider _compositeFileProvider;

    /// <summary>
    /// 当文件发生变化时触发的事件
    /// </summary>
    /// <remarks>
    /// 当任何文件提供者中的文件发生变化时，都会触发此事件
    /// </remarks>
    public event EventHandler<FileChangeEventArgs> FileChanged;

    /// <summary>
    /// 初始化虚拟文件系统管理器
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="options">配置选项</param>
    public VirtualFileSystemManager(
        IServiceProvider serviceProvider,
        IOptions<VirtualFileSystemOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _fileProviders = new List<IVirtualFileProvider>();

        Initialize();
    }

    private void Initialize()
    {
        // 添加内存文件提供者
        AddProvider(new InMemoryFileProvider());

        // 添加嵌入式资源文件提供者
        foreach (var assembly in _options.EmbeddedAssemblies)
        {
            AddProvider(new EmbeddedFileProvider(assembly, string.Empty));
        }

        // 添加物理文件提供者
        foreach (var path in _options.PhysicalPaths)
        {
            AddProvider(new PhysicalVirtualFileProvider(path));
        }

        // 初始化组合文件提供者
        _compositeFileProvider = new CompositeFileProvider(_fileProviders.ToArray());
    }

    /// <summary>
    /// 添加文件提供者
    /// </summary>
    /// <param name="provider">要添加的文件提供者</param>
    /// <remarks>
    /// 添加新的文件提供者后，会自动重新构建组合文件提供者，并设置文件变更监听
    /// </remarks>
    public void AddProvider(IVirtualFileProvider provider)
    {
        _fileProviders.Add(provider);
        _compositeFileProvider = new CompositeFileProvider(_fileProviders.ToArray());

        // 监听文件变化
        var token = provider.Watch("*");
        RegisterChangeCallback(token, () =>
        {
            FileChanged?.Invoke(this, new FileChangeEventArgs("*", WatcherChangeTypes.Changed));
        });
    }

    private IDisposable RegisterChangeCallback(IChangeToken changeToken, Action callback)
    {
        if (changeToken == null)
        {
            throw new ArgumentNullException(nameof(changeToken));
        }

        return changeToken.RegisterChangeCallback(_ => callback(), null);
    }

    /// <summary>
    /// 获取虚拟文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>虚拟文件对象，如果文件不存在则返回 null</returns>
    /// <remarks>
    /// 按照文件提供者的添加顺序依次查找文件，返回第一个找到的文件
    /// </remarks>
    public IVirtualFile GetFile(string path)
    {
        foreach (var provider in _fileProviders)
        {
            var file = provider.GetVirtualFile(path);
            if (file != null)
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <returns>目录内容对象</returns>
    /// <remarks>
    /// 返回组合文件提供者中的所有匹配目录内容
    /// </remarks>
    public IDirectoryContents GetDirectoryContents(string path)
    {
        return _compositeFileProvider.GetDirectoryContents(path);
    }

    /// <summary>
    /// 获取文件提供者
    /// </summary>
    /// <returns>组合文件提供者</returns>
    /// <remarks>
    /// 返回包含所有已添加文件提供者的组合文件提供者
    /// </remarks>
    public IFileProvider GetFileProvider()
    {
        return _compositeFileProvider;
    }

    /// <summary>
    /// 添加文件监视
    /// </summary>
    /// <param name="filter">文件筛选器</param>
    /// <returns>文件监视注册</returns>
    public IDisposable Watch(string filter)
    {
        var changeToken = GetFileProvider().Watch(filter);
        return RegisterChangeCallback(changeToken, () =>
        {
            FileChanged?.Invoke(this, new FileChangeEventArgs(filter, WatcherChangeTypes.Changed));
        });
    }

    /// <summary>
    /// 触发文件变更事件
    /// </summary>
    /// <param name="args"></param>
    public virtual void OnFileChanged(FileChangeEventArgs args)
    {
        FileChanged?.Invoke(this, args);
    }
}
