#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemServiceCollectionExtensions
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:26:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统服务集合扩展
/// </summary>
public static class VirtualFileSystemServiceCollectionExtensions
{
    /// <summary>
    /// 添加虚拟文件系统服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置选项</param>
    /// <returns></returns>
    public static IServiceCollection AddVirtualFileSystem(
        this IServiceCollection services,
        Action<VirtualFileSystemOptions>? configure = null)
    {
        services.Configure<VirtualFileSystemOptions>(options =>
        {
            configure?.Invoke(options);
        });

        services.TryAddSingleton<IVirtualFileSystemManager, VirtualFileSystemManager>();
        services.TryAddTransient<IVirtualFileSystemOperations, VirtualFileSystemOperations>();

        return services;
    }

    /// <summary>
    /// 添加物理文件系统
    /// </summary>
    public static IServiceCollection AddPhysicalFileSystem(
        this IServiceCollection services,
        string rootPath,
        Action<PhysicalFileProviderOptions>? configure = null)
    {
        if (!Directory.Exists(rootPath))
        {
            Directory.CreateDirectory(rootPath);
        }

        var options = new PhysicalFileProviderOptions();
        configure?.Invoke(options);

        services.Configure<VirtualFileSystemOptions>(vfsOptions =>
        {
            // 创建物理文件提供器
            var physicalFileProvider = new PhysicalFileProvider(rootPath);
            vfsOptions.AddFileProvider(physicalFileProvider);

            // 如果启用了文件监视，设置文件系统监视器
            if (options.EnableFileWatch)
            {
                var fileSystemWatcher = new FileSystemWatcher(rootPath)
                {
                    Filter = options.FileWatchFilter,
                    IncludeSubdirectories = options.IncludeSubdirectories,
                    EnableRaisingEvents = true,
                    InternalBufferSize = options.BufferSize
                };

                fileSystemWatcher.Changed += (sender, args) =>
                {
                    var manager = services.BuildServiceProvider().GetRequiredService<IVirtualFileSystemManager>();
                    if (manager is VirtualFileSystemManager virtualFileSystemManager)
                    {
                        virtualFileSystemManager.OnFileChanged(new FileChangeEventArgs(args.FullPath, (WatcherChangeTypes)args.ChangeType));
                    }
                };

                // 添加其他事件处理
                fileSystemWatcher.Created += (sender, args) =>
                {
                    var manager = services.BuildServiceProvider().GetRequiredService<IVirtualFileSystemManager>();
                    if (manager is VirtualFileSystemManager virtualFileSystemManager)
                    {
                        virtualFileSystemManager.OnFileChanged(new FileChangeEventArgs(args.FullPath, WatcherChangeTypes.Created));
                    }
                };

                fileSystemWatcher.Deleted += (sender, args) =>
                {
                    var manager = services.BuildServiceProvider().GetRequiredService<IVirtualFileSystemManager>();
                    if (manager is VirtualFileSystemManager virtualFileSystemManager)
                    {
                        virtualFileSystemManager.OnFileChanged(new FileChangeEventArgs(args.FullPath, WatcherChangeTypes.Deleted));
                    }
                };

                fileSystemWatcher.Renamed += (sender, args) =>
                {
                    var manager = services.BuildServiceProvider().GetRequiredService<IVirtualFileSystemManager>();
                    if (manager is VirtualFileSystemManager virtualFileSystemManager)
                    {
                        virtualFileSystemManager.OnFileChanged(new FileChangeEventArgs(args.FullPath, WatcherChangeTypes.Renamed));
                    }
                };
            }
        });

        return services;
    }

    /// <summary>
    /// 添加嵌入式文件系统
    /// </summary>
    public static IServiceCollection AddEmbeddedFileSystem<T>(
        this IServiceCollection services,
        string baseNamespace)
    {
        services.Configure<VirtualFileSystemOptions>(options =>
        {
            var assembly = typeof(T).Assembly;
            var embeddedFileProvider = new EmbeddedFileProvider(assembly, baseNamespace);
            options.AddFileProvider(embeddedFileProvider);
        });

        return services;
    }
}
