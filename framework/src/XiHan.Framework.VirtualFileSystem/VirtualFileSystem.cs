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
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Utils.Threading;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Providers.Composite;
using XiHan.Framework.VirtualFileSystem.Utilities;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统核心实现
/// </summary>
public class VirtualFileSystem : IVirtualFileSystem, IDisposable
{
    private readonly Debouncer _changeDebouncer;

    private readonly List<PrioritizedFileProvider> _providers = [];
    private IFileProvider _compositeProvider;

    /// <summary>
    /// 初始化虚拟文件系统
    /// </summary>
    /// <param name="providers">初始文件提供程序集合</param>
    public VirtualFileSystem(IEnumerable<IFileProvider> providers)
    {
        _changeDebouncer = new(TimeSpan.FromMilliseconds(500));
        _compositeProvider = new VirtualCompositeFileProvider(
            providers.Select(p => new PrioritizedFileProvider(p, 0))
        );
    }

    /// <summary>
    /// 异步获取文件信息
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<IFileInfo> GetFileAsync(string virtualPath, CancellationToken ct = default)
    {
        return Task.FromResult(GetFile(virtualPath));
    }

    /// <summary>
    /// 异步获取目录内容
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<IDirectoryContents> GetDirectoryContentsAsync(string virtualPath, CancellationToken ct = default)
    {
        return Task.FromResult(GetDirectoryContents(virtualPath));
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
            if (originalToken.HasChanged)
            {
                //TODO: 通知变化
            }
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
        (_compositeProvider as IDisposable)?.Dispose();
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
}
