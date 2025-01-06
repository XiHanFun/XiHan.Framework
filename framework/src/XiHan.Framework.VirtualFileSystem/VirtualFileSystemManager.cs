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

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统管理器
/// </summary>
public class VirtualFileSystemManager : IVirtualFileSystemManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VirtualFileSystemOptions _options;
    private readonly Lazy<IFileProvider> _compositeFileProvider;

    /// <summary>
    /// 文件变更事件
    /// </summary>
    public event EventHandler<FileChangeEventArgs>? FileChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="options"></param>
    public VirtualFileSystemManager(
        IServiceProvider serviceProvider,
        IOptions<VirtualFileSystemOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _compositeFileProvider = new Lazy<IFileProvider>(CreateCompositeFileProvider, true);
    }

    /// <summary>
    /// 获取当前文件提供器
    /// </summary>
    public IFileProvider GetFileProvider()
    {
        return _compositeFileProvider.Value;
    }

    /// <summary>
    /// 添加文件监视
    /// </summary>
    public IDisposable Watch(string filter)
    {
        var changeToken = GetFileProvider().Watch(filter);
        return ChangeToken.OnChange(() => GetFileProvider().Watch(filter), () =>
        {
            OnFileChanged(new FileChangeEventArgs(filter, WatcherChangeTypes.Modified));
        });
    }

    /// <summary>
    /// 创建组合文件提供器
    /// </summary>
    /// <returns></returns>
    private IFileProvider CreateCompositeFileProvider()
    {
        return new CompositeFileProvider(_options.FileProviders);
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
