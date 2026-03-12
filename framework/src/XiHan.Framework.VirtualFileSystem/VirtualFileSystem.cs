#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystem
// Guid:61abcff1-2c5b-4e9c-bc42-c978bf35085e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 05:37:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Threading;
using XiHan.Framework.VirtualFileSystem.Events;
using XiHan.Framework.VirtualFileSystem.Options;
using XiHan.Framework.VirtualFileSystem.Providers;
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
    private readonly Lock _syncLock = new();
    private readonly List<PrioritizedFileProvider> _providers = [];
    private readonly HashSet<string> _physicalPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Assembly> _embeddedAssemblies = [];
    private readonly ConcurrentDictionary<string, DateTime> _fileStateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Regex> _watchRegexCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Debouncer _changeDebouncer;
    private readonly bool _enableChangeTracking;
    private IFileProvider _compositeProvider = new NullFileProvider();
    private bool _disposed;

    /// <summary>
    /// 初始化虚拟文件系统
    /// </summary>
    /// <param name="options">虚拟文件系统配置</param>
    public VirtualFileSystem(IOptions<VirtualFileSystemOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var vfsOptions = options.Value;

            _enableChangeTracking = vfsOptions.EnableChangeTracking;
            _changeDebouncer = new Debouncer(
                TimeSpan.FromMilliseconds(Math.Max(vfsOptions.ChangeDebounceMilliseconds, 50))
            );

            foreach (var provider in vfsOptions.Providers.OrderByDescending(x => x.Priority))
            {
                RegisterProvider(provider.Provider, provider.Priority, replaceExisting: true);
            }

            RebuildCompositeProvider();
        }
        catch (Exception ex)
        {
            throw new XiHanException("虚拟文件系统初始化失败", ex);
        }
    }

    /// <summary>
    /// 文件变化事件
    /// </summary>
    public event EventHandler<FileChangedEventArgs> OnFileChanged = delegate { };

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
    /// 监听文件变化
    /// </summary>
    public IChangeToken Watch(string filter)
    {
        ThrowIfDisposed();
        var normalizedFilter = NormalizeFilter(filter);
        return RegisterWatch(normalizedFilter);
    }

    /// <summary>
    /// 挂载文件提供程序
    /// </summary>
    public void Mount(IFileProvider provider, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ThrowIfDisposed();

        lock (_syncLock)
        {
            RegisterProvider(provider, priority, replaceExisting: true);
            RebuildCompositeProvider();
        }
    }

    /// <summary>
    /// 卸载文件提供程序
    /// </summary>
    public bool Unmount(IFileProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ThrowIfDisposed();

        lock (_syncLock)
        {
            var key = GetProviderKey(provider);
            var existing = _providers.FirstOrDefault(x => GetProviderKey(x.Provider) == key);
            if (existing is null)
            {
                return false;
            }

            _providers.Remove(existing);
            UnregisterProviderChangeTracking(existing.Provider);
            RebuildCompositeProvider();
            return true;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _changeDebouncer.Dispose();
        (_compositeProvider as IDisposable)?.Dispose();
        _providers.Clear();
        _physicalPaths.Clear();
        _embeddedAssemblies.Clear();
        _fileStateCache.Clear();
        _watchRegexCache.Clear();
        GC.SuppressFinalize(this);
    }

    #region 私有方法

    private IChangeToken RegisterWatch(string normalizedFilter)
    {
        var token = _compositeProvider.Watch(normalizedFilter);
        token.RegisterChangeCallback(_ =>
        {
            if (_disposed)
            {
                return;
            }

            _changeDebouncer.Debounce(() =>
            {
                if (_disposed)
                {
                    return;
                }

                var changedFiles = GetChangedFiles(normalizedFilter).ToList();
                foreach (var (path, changeType) in changedFiles)
                {
                    OnFileChanged.Invoke(this, new FileChangedEventArgs(path, changeType));
                }
            });

            RegisterWatch(normalizedFilter);
        }, null);

        return token;
    }

    private void RegisterProvider(IFileProvider provider, int priority, bool replaceExisting)
    {
        var key = GetProviderKey(provider);
        var existing = _providers.FirstOrDefault(x => GetProviderKey(x.Provider) == key);
        if (existing != null)
        {
            if (!replaceExisting)
            {
                return;
            }

            _providers.Remove(existing);
            UnregisterProviderChangeTracking(existing.Provider);
        }

        _providers.Add(new PrioritizedFileProvider(provider, priority));
        RegisterProviderChangeTracking(provider);
    }

    private void RegisterProviderChangeTracking(IFileProvider provider)
    {
        if (!_enableChangeTracking)
        {
            return;
        }

        if (provider is PhysicalFileProvider physicalProvider)
        {
            RegisterPhysicalProvider(physicalProvider.Root);
            return;
        }

        if (provider is VirtualEmbeddedFileProvider embeddedProvider)
        {
            RegisterEmbeddedProvider(embeddedProvider.Assembly);
        }
    }

    private void UnregisterProviderChangeTracking(IFileProvider provider)
    {
        if (!_enableChangeTracking)
        {
            return;
        }

        if (provider is PhysicalFileProvider physicalProvider)
        {
            _physicalPaths.Remove(NormalizePhysicalPath(physicalProvider.Root));
            return;
        }

        if (provider is VirtualEmbeddedFileProvider embeddedProvider)
        {
            _embeddedAssemblies.Remove(embeddedProvider.Assembly);
        }
    }

    private void RegisterPhysicalProvider(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        var normalizedPath = NormalizePhysicalPath(rootPath);
        if (!_physicalPaths.Add(normalizedPath))
        {
            return;
        }

        if (!Directory.Exists(normalizedPath))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(normalizedPath, "*", SearchOption.AllDirectories))
        {
            var normalizedFile = NormalizePhysicalPath(file);
            _fileStateCache[normalizedFile] = File.GetLastWriteTimeUtc(normalizedFile);
        }
    }

    private void RegisterEmbeddedProvider(Assembly assembly)
    {
        if (!_embeddedAssemblies.Add(assembly))
        {
            return;
        }

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            _fileStateCache[GetEmbeddedCacheKey(assembly, resourceName)] = DateTime.UtcNow;
        }
    }

    private IEnumerable<(string Path, FileChangeType ChangeType)> GetChangedFiles(string filter)
    {
        var changes = new List<(string Path, FileChangeType ChangeType)>();
        if (!_enableChangeTracking)
        {
            return changes;
        }

        var normalizedFilter = NormalizeFilter(filter);

        foreach (var physicalPath in _physicalPaths)
        {
            var files = Directory.Exists(physicalPath)
                ? Directory.GetFiles(physicalPath, "*", SearchOption.AllDirectories)
                : [];

            var currentFiles = new HashSet<string>(
                files.Select(NormalizePhysicalPath),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var file in currentFiles)
            {
                if (!IsPhysicalFileMatchFilter(physicalPath, file, normalizedFilter))
                {
                    continue;
                }

                var currentWriteTime = File.GetLastWriteTimeUtc(file);
                if (!_fileStateCache.TryGetValue(file, out var previousWriteTime))
                {
                    _fileStateCache[file] = currentWriteTime;
                    changes.Add((file, FileChangeType.Created));
                }
                else if (previousWriteTime != currentWriteTime)
                {
                    _fileStateCache[file] = currentWriteTime;
                    changes.Add((file, FileChangeType.Modified));
                }
            }

            var deletedFiles = _fileStateCache.Keys
                .Where(x => x.StartsWith(physicalPath, StringComparison.OrdinalIgnoreCase))
                .Where(x => !currentFiles.Contains(x))
                .Where(x => IsPhysicalFileMatchFilter(physicalPath, x, normalizedFilter))
                .ToList();

            foreach (var deletedFile in deletedFiles)
            {
                _fileStateCache.Remove(deletedFile, out _);
                changes.Add((deletedFile, FileChangeType.Deleted));
            }
        }

        foreach (var assembly in _embeddedAssemblies)
        {
            var currentResourceNames = assembly.GetManifestResourceNames()
                .Where(x => IsEmbeddedResourceMatchFilter(x, normalizedFilter))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceName in currentResourceNames)
            {
                var cacheKey = GetEmbeddedCacheKey(assembly, resourceName);
                if (_fileStateCache.ContainsKey(cacheKey))
                {
                    continue;
                }

                _fileStateCache[cacheKey] = DateTime.UtcNow;
                changes.Add((GetEmbeddedVirtualPath(assembly, resourceName), FileChangeType.Created));
            }

            var deletedResources = _fileStateCache.Keys
                .Where(x => x.StartsWith($"{assembly.FullName}::", StringComparison.OrdinalIgnoreCase))
                .Where(x => !currentResourceNames.Contains(GetEmbeddedResourceNameFromCacheKey(x)))
                .ToList();

            foreach (var deletedResource in deletedResources)
            {
                var resourceName = GetEmbeddedResourceNameFromCacheKey(deletedResource);
                _fileStateCache.Remove(deletedResource, out _);
                changes.Add((GetEmbeddedVirtualPath(assembly, resourceName), FileChangeType.Deleted));
            }
        }

        return changes;
    }

    private void RebuildCompositeProvider()
    {
        var orderedProviders = _providers
            .OrderByDescending(x => x.Priority)
            .ToList();

        (_compositeProvider as IDisposable)?.Dispose();
        _compositeProvider = orderedProviders.Count == 0
            ? new NullFileProvider()
            : new VirtualCompositeFileProvider(orderedProviders);
    }

    private bool IsPhysicalFileMatchFilter(string rootPath, string fullFilePath, string filter)
    {
        var relativePath = "/" + Path.GetRelativePath(rootPath, fullFilePath).Replace('\\', '/');
        return IsPathMatch(relativePath, filter) || IsPathMatch(Path.GetFileName(fullFilePath), filter);
    }

    private bool IsEmbeddedResourceMatchFilter(string resourceName, string filter)
    {
        var normalizedResourcePath = "/" + resourceName.Replace('.', '/');
        return IsPathMatch(normalizedResourcePath, filter)
            || IsPathMatch(resourceName, filter)
            || IsPathMatch(Path.GetFileName(normalizedResourcePath), filter);
    }

    private bool IsPathMatch(string path, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter == "*" || filter == "**/*")
        {
            return true;
        }

        var regex = _watchRegexCache.GetOrAdd(filter, CreateWatchRegex);
        var normalizedPath = path.Replace('\\', '/');
        return regex.IsMatch(normalizedPath);
    }

    private static Regex CreateWatchRegex(string filter)
    {
        var normalizedFilter = NormalizeFilter(filter);
        var regexPattern = Regex.Escape(normalizedFilter)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/]*")
            .Replace(@"\?", ".");

        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static string NormalizeFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return "**/*";
        }

        return filter.Trim().Replace('\\', '/');
    }

    private static string NormalizePhysicalPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string GetProviderKey(IFileProvider provider)
    {
        return provider switch
        {
            VirtualPhysicalFileProvider virtualPhysicalProvider => $"physical:{NormalizePhysicalPath(virtualPhysicalProvider.Root)}",
            PhysicalFileProvider physicalProvider => $"physical:{NormalizePhysicalPath(physicalProvider.Root)}",
            VirtualEmbeddedFileProvider virtualEmbeddedProvider => $"embedded:{virtualEmbeddedProvider.Assembly.FullName}",
            _ => $"instance:{provider.GetType().FullName}:{provider.GetHashCode()}"
        };
    }

    private static string GetEmbeddedCacheKey(Assembly assembly, string resourceName)
    {
        return $"{assembly.FullName}::{resourceName}";
    }

    private static string GetEmbeddedResourceNameFromCacheKey(string cacheKey)
    {
        var separatorIndex = cacheKey.IndexOf("::", StringComparison.Ordinal);
        return separatorIndex < 0
            ? cacheKey
            : cacheKey[(separatorIndex + 2)..];
    }

    private static string GetEmbeddedVirtualPath(Assembly assembly, string resourceName)
    {
        return $"embedded://{assembly.GetName().Name}/{resourceName}";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion 私有方法
}
