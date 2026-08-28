// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Text;
using XiHan.Framework.VirtualFileSystem;
using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.Localization.Tests.TestSupport;

/// <summary>
/// 内存版虚拟文件系统替身
/// </summary>
/// <remarks>
/// 本地化资源存储只依赖 EnumerateFiles / GetFile / Watch 以及文件变化事件这四处能力，
/// 这里用内存字典实现，避免真实文件系统的目录扫描与防抖计时带来的用例不确定性。
/// 额外暴露 <see cref="WatchFilters"/> 与 <see cref="FileChangedSubscriberCount"/>，
/// 用于断言资源存储确实登记了监控、并在释放时解绑了事件。
/// </remarks>
public sealed class FakeVirtualFileSystem : IVirtualFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private EventHandler<FileChangedEventArgs>? _fileChanged;

    /// <summary>
    /// 文件变化事件
    /// </summary>
    public event EventHandler<FileChangedEventArgs> OnFileChanged
    {
        add => _fileChanged += value;
        remove => _fileChanged -= value;
    }

    /// <summary>
    /// 已登记的监控过滤条件
    /// </summary>
    public List<string> WatchFilters { get; } = [];

    /// <summary>
    /// 当前文件变化事件的订阅者数量
    /// </summary>
    public int FileChangedSubscriberCount => _fileChanged?.GetInvocationList().Length ?? 0;

    /// <summary>
    /// 写入一个虚拟文件
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <param name="content">文件内容</param>
    public void AddFile(string virtualPath, string content)
    {
        _files[virtualPath] = content;
    }

    /// <summary>
    /// 移除一个虚拟文件
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    public void RemoveFile(string virtualPath)
    {
        _files.Remove(virtualPath);
    }

    /// <summary>
    /// 手动触发文件变化事件
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <param name="changeType">变化类型</param>
    public void RaiseFileChanged(string virtualPath, FileChangeType changeType)
    {
        _fileChanged?.Invoke(this, new FileChangedEventArgs(virtualPath, changeType));
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>文件信息</returns>
    public IFileInfo GetFile(string virtualPath)
    {
        var name = Path.GetFileName(virtualPath);
        return _files.TryGetValue(virtualPath, out var content)
            ? new InMemoryFileInfo(name, content)
            : new MissingFileInfo(name);
    }

    /// <summary>
    /// 获取目录内容（本替身不参与目录枚举，恒返回空内容）
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>目录内容</returns>
    public IDirectoryContents GetDirectoryContents(string virtualPath)
    {
        return new EmptyDirectoryContents();
    }

    /// <summary>
    /// 判断文件是否存在
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>是否存在</returns>
    public bool FileExists(string virtualPath)
    {
        return _files.ContainsKey(virtualPath);
    }

    /// <summary>
    /// 判断目录是否存在
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>是否存在</returns>
    public bool DirectoryExists(string virtualPath)
    {
        var root = TrimTrailingSlash(virtualPath);
        return _files.Keys.Any(x => root.Length == 0 || x.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 枚举目录下的文件
    /// </summary>
    /// <param name="virtualPath">虚拟目录路径</param>
    /// <param name="searchPattern">搜索模式</param>
    /// <param name="recursive">是否递归</param>
    /// <returns>虚拟文件路径集合</returns>
    public IReadOnlyList<string> EnumerateFiles(string virtualPath, string searchPattern = "*", bool recursive = true)
    {
        var root = TrimTrailingSlash(virtualPath);
        return _files.Keys
            .Where(x => root.Length == 0 || x.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            .Where(x => IsPatternMatch(x, searchPattern))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 登记文件监控
    /// </summary>
    /// <param name="filter">监控过滤条件</param>
    /// <returns>变化令牌</returns>
    public IChangeToken Watch(string filter)
    {
        WatchFilters.Add(filter);
        return new InertChangeToken();
    }

    /// <summary>
    /// 挂载文件提供程序（本替身不支持挂载）
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <param name="priority">优先级</param>
    public void Mount(IFileProvider provider, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(provider);
    }

    /// <summary>
    /// 卸载文件提供程序（本替身不支持挂载）
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <returns>是否卸载成功</returns>
    public bool Unmount(IFileProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return false;
    }

    private static string TrimTrailingSlash(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.TrimEnd('/');
    }

    private static bool IsPatternMatch(string path, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(searchPattern) || searchPattern == "*")
        {
            return true;
        }

        return searchPattern.StartsWith('*')
            ? path.EndsWith(searchPattern[1..], StringComparison.OrdinalIgnoreCase)
            : path.EndsWith(searchPattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 内存文件信息
    /// </summary>
    private sealed class InMemoryFileInfo : IFileInfo
    {
        private readonly byte[] _content;

        public InMemoryFileInfo(string name, string content)
        {
            Name = name;
            _content = Encoding.UTF8.GetBytes(content);
        }

        public bool Exists => true;

        public long Length => _content.Length;

        public string? PhysicalPath => null;

        public string Name { get; }

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            return new MemoryStream(_content, writable: false);
        }
    }

    /// <summary>
    /// 不存在的文件信息
    /// </summary>
    private sealed class MissingFileInfo : IFileInfo
    {
        public MissingFileInfo(string name)
        {
            Name = name;
        }

        public bool Exists => false;

        public long Length => -1;

        public string? PhysicalPath => null;

        public string Name { get; }

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            throw new FileNotFoundException(Name);
        }
    }

    /// <summary>
    /// 空目录内容
    /// </summary>
    private sealed class EmptyDirectoryContents : IDirectoryContents
    {
        public bool Exists => false;

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            return Enumerable.Empty<IFileInfo>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// 永不触发的变化令牌
    /// </summary>
    private sealed class InertChangeToken : IChangeToken
    {
        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return NoopDisposable.Instance;
        }
    }

    /// <summary>
    /// 空释放器
    /// </summary>
    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
