#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystem
// Guid:61abcff1-2c5b-4e9c-bc42-c978bf35085e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 5:37:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Threading;
using XiHan.Framework.VirtualFileSystem.Events;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Providers.Composite;
using XiHan.Framework.VirtualFileSystem.Providers.Embedded;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;
using XiHan.Framework.VirtualFileSystem.Utilities;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统核心实现
/// </summary>
public class VirtualFileSystem : IVirtualFileSystem, IDisposable
{
    private readonly List<PrioritizedFileProvider> _providers = [];
    private IFileProvider _compositeProvider;
    private readonly Debouncer _changeDebouncer;
    private readonly List<string> _physicalPaths = [];
    private readonly List<Type> _embeddedResourceTypes = [];
    private readonly ConcurrentDictionary<string, DateTime> _fileStateCache = [];

    /// <summary>
    /// 文件变化事件
    /// </summary>
    public event EventHandler<FileChangedEventArgs> OnFileChanged = delegate { };

    /// <summary>
    /// 初始化虚拟文件系统
    /// </summary>
    /// <param name="options">虚拟文件系统配置选项</param>
    public VirtualFileSystem(IOptions<VirtualFileSystemOptions> options)
    {
        try
        {
            var virtualFileSystemOptions = options.Value;
            var providers = virtualFileSystemOptions.Providers
                .OrderByDescending(p => p.Priority)
                .Select(p => p.Provider)
                .ToList();

            _changeDebouncer = new(TimeSpan.FromMilliseconds(500));
            _compositeProvider = new VirtualCompositeFileProvider(
                providers.Select(p => new PrioritizedFileProvider(p, 0))
            );

            foreach (var provider in providers)
            {
                if (provider is VirtualPhysicalFileProvider physicalProvider)
                {
                    _physicalPaths.Add(physicalProvider.Root);
                    // 初始化时记录已存在的文件
                    var files = Directory.GetFiles(physicalProvider.Root, "*.*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        _fileStateCache[file] = File.GetLastWriteTime(file);
                    }
                }
                else if (provider is VirtualEmbeddedFileProvider embeddedProvider)
                {
                    _embeddedResourceTypes.Add(embeddedProvider.Assembly.GetType());
                }
            }
        }
        catch (Exception ex)
        {
            throw new XiHanException("虚拟文件系统初始化失败", ex);
        }
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    public IFileInfo GetFile(string virtualPath)
    {
        var resolvedPath = PathResolver.ResolveVirtualPath(virtualPath);
        return _compositeProvider.GetFileInfo(resolvedPath);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    public IDirectoryContents GetDirectoryContents(string virtualPath)
    {
        var resolvedPath = PathResolver.ResolveVirtualPath(virtualPath);
        return _compositeProvider.GetDirectoryContents(resolvedPath);
    }

    /// <summary>
    /// 监控文件变化
    /// </summary>
    public IChangeToken Watch(string filter)
    {
        // 获取原始变化令牌
        var originalToken = _compositeProvider.Watch(filter);

        // 应用防抖处理
        _changeDebouncer.Debounce(() =>
        {
            // 注册回调以触发事件
            _ = originalToken.RegisterChangeCallback(_ =>
            {
                // 获取变化的文件列表
                var changedFiles = GetChangedFiles(filter);

                // 遍历变化的文件并触发事件
                foreach (var file in changedFiles)
                {
                    OnFileChanged?.Invoke(this, new FileChangedEventArgs(file.Path, file.ChangeType));
                }

                // 重新注册监听，实现持续监控
                _ = Watch(filter);
            }, null);
        });
        return originalToken;
    }

    /// <summary>
    /// 挂载文件提供程序
    /// </summary>
    public void Mount(IFileProvider provider, int priority = 0)
    {
        lock (_providers)
        {
            _providers.Add(new PrioritizedFileProvider(provider, priority));
            RebuildCompositeProvider();
        }
    }

    /// <summary>
    /// 卸载文件提供程序
    /// </summary>
    public bool Unmount(IFileProvider provider)
    {
        lock (_providers)
        {
            var item = _providers.FirstOrDefault(p => p.Provider == provider);
            if (item != null)
            {
                _ = _providers.Remove(item);
                RebuildCompositeProvider();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        try
        {
            // 释放防抖器资源
            _changeDebouncer.Dispose();

            // 释放组合文件提供程序资源
            (_compositeProvider as IDisposable)?.Dispose();

            // 清空文件状态缓存
            _fileStateCache.Clear();

            // 清空物理路径列表
            _physicalPaths.Clear();

            // 清空嵌入资源类型列表
            _embeddedResourceTypes.Clear();

            // 清空文件提供程序集合
            _providers.Clear();
        }
        catch (Exception ex)
        {
            throw new XiHanException("释放虚拟文件系统资源时发生异常", ex);
        }
    }

    #region 私有方法

    /// <summary>
    /// 获取变化的文件列表
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    private IEnumerable<(string Path, FileChangeType ChangeType)> GetChangedFiles(string filter)
    {
        var changedFiles = new List<(string Path, FileChangeType ChangeType)>();

        // 检查物理路径中的文件变化
        foreach (var physicalPath in _physicalPaths)
        {
            var files = Directory.GetFiles(physicalPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (!_fileStateCache.TryGetValue(file, out var lastWriteTime))
                {
                    changedFiles.Add((file, FileChangeType.Created));
                    _fileStateCache[file] = fileInfo.LastWriteTime;
                }
                else if (lastWriteTime != fileInfo.LastWriteTime)
                {
                    // 修改文件
                    changedFiles.Add((file, FileChangeType.Modified));
                    _fileStateCache[file] = fileInfo.LastWriteTime;
                }
            }

            // 检查删除的文件
            var deletedFiles = _fileStateCache.Keys
                .Where(k => k.StartsWith(physicalPath) && !files.Contains(k))
                .ToList();
            foreach (var file in deletedFiles)
            {
                changedFiles.Add((file, FileChangeType.Deleted));
                _ = _fileStateCache.Remove(file, out _);
            }
        }

        // 检查嵌入资源中的文件变化
        foreach (var type in _embeddedResourceTypes)
        {
            var assembly = type.Assembly;
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(filter, StringComparison.OrdinalIgnoreCase));

            foreach (var resourceName in resourceNames)
            {
                if (!_fileStateCache.ContainsKey(resourceName))
                {
                    // 新增资源
                    changedFiles.Add((resourceName, FileChangeType.Created));
                    // 嵌入资源无法获取修改时间，使用当前时间
                    _fileStateCache[resourceName] = DateTime.UtcNow;
                }
            }

            // 检查删除的资源
            var deletedResources = _fileStateCache.Keys
                .Where(k => k.StartsWith(assembly.GetName().Name!) && !resourceNames.Contains(k))
                .ToList();
            foreach (var resource in deletedResources)
            {
                changedFiles.Add((resource, FileChangeType.Deleted));
                _ = _fileStateCache.Remove(resource, out _);
            }
        }

        return changedFiles;
    }

    /// <summary>
    /// 重新构建组合文件提供程序
    /// </summary>
    private void RebuildCompositeProvider()
    {
        var orderedProviders = _providers
            .OrderByDescending(p => p.Priority)
            .Select(p => p.Provider)
            .ToList();

        (_compositeProvider as IDisposable)?.Dispose();
        _compositeProvider = new VirtualCompositeFileProvider(
            orderedProviders.Select(p => new PrioritizedFileProvider(p, 0))
        );
    }

    #endregion 私有方法
}
