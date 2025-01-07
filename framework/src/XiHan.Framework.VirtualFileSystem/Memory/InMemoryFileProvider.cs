#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryFileProvider
// Guid:d4f1c67c-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:14:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Collections.Concurrent;
using XiHan.Framework.Utils.IO;
using XiHan.Framework.Utils.System;
using XiHan.Framework.VirtualFileSystem.Core;

namespace XiHan.Framework.VirtualFileSystem.Memory;

/// <summary>
/// 内存文件提供者
/// </summary>
/// <remarks>
/// 提供基于内存的文件存储实现，支持文件的添加、删除和监视
/// </remarks>
public class InMemoryFileProvider : IVirtualFileProvider
{
    private readonly ConcurrentDictionary<string, IVirtualFile> _files;
    private readonly ConcurrentDictionary<string, List<Action<string>>> _watchers;

    /// <summary>
    /// 初始化内存文件提供者
    /// </summary>
    public InMemoryFileProvider()
    {
        _files = new ConcurrentDictionary<string, IVirtualFile>(StringComparer.OrdinalIgnoreCase);
        _watchers = new ConcurrentDictionary<string, List<Action<string>>>();
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>文件信息对象</returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        return GetVirtualFile(subpath);
    }

    /// <summary>
    /// 获取虚拟文件
    /// </summary>
    /// <param name="subpath">文件路径</param>
    /// <returns>虚拟文件对象</returns>
    public IVirtualFile GetVirtualFile(string subpath)
    {
        _files.TryGetValue(subpath, out var file);
        return file;
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath">目录路径</param>
    /// <returns>目录内容对象</returns>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (!DirectoryHelper.Exists(subpath))
        {
            return new NotFoundDirectoryContents();
        }

        var directory = new List<IFileInfo>();
        var prefix = subpath.TrimEnd('/') + '/';

        foreach (var file in _files.Values)
        {
            var filePath = file.PhysicalPath ?? file.Name;
            if (filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                directory.Add(file);
            }
        }

        return new InMemoryDirectoryContents(directory);
    }

    /// <summary>
    /// 监视文件变化
    /// </summary>
    /// <param name="filter">文件筛选器</param>
    /// <returns>变更令牌</returns>
    public IChangeToken Watch(string filter)
    {
        var watchers = _watchers.GetOrAdd(filter, _ => new List<Action<string>>());
        return new ChangeToken(watchers);
    }

    /// <summary>
    /// 添加文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    public void AddFile(string path, Stream stream)
    {
        var normalizedPath = path.Replace('\\', '/');
        var file = new InMemoryFile(normalizedPath, stream);
        _files.AddOrUpdate(normalizedPath, file, (_, __) => file);
        NotifyChange(normalizedPath);
    }

    /// <summary>
    /// 移除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    public void RemoveFile(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        if (_files.TryRemove(normalizedPath, out _))
        {
            NotifyChange(normalizedPath);
        }
    }

    private void NotifyChange(string path)
    {
        foreach (var watcher in _watchers)
        {
            if (MatchesFilter(path, watcher.Key))
            {
                foreach (var callback in watcher.Value)
                {
                    callback(path);
                }
            }
        }
    }

    private static bool MatchesFilter(string path, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return false;
        }

        if (filter.Contains("*"))
        {
            return path.StartsWith(filter.TrimEnd('*'), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(path, filter, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 内存文件实现
/// </summary>
internal class InMemoryFile : IVirtualFile, IFileInfo
{
    private readonly byte[] _content;

    /// <summary>
    /// 初始化内存文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    public InMemoryFile(string path, Stream stream)
    {
        Path = path.Replace('\\', '/');
        Name = System.IO.Path.GetFileName(path);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        _content = ms.ToArray();
        Length = _content.Length;
        LastModified = DateTimeOffset.UtcNow;
        IsDirectory = false;
        Exists = true;
        PhysicalPath = null;
    }

    /// <summary>
    /// 获取文件名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取文件路径
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 获取文件大小（字节）
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// 获取最后修改时间
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    /// 获取是否是目录
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// 获取文件是否存在
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// 获取物理文件路径
    /// </summary>
    /// <remarks>
    /// 内存文件没有物理路径，始终返回 null
    /// </remarks>
    public string PhysicalPath { get; }

    /// <summary>
    /// 创建文件读取流
    /// </summary>
    /// <returns>文件流</returns>
    public Stream CreateReadStream()
    {
        return new MemoryStream(_content);
    }

    /// <summary>
    /// 异步创建文件读取流
    /// </summary>
    /// <returns>文件流任务</returns>
    public Task<Stream> CreateReadStreamAsync()
    {
        return Task.FromResult<Stream>(new MemoryStream(_content));
    }
}

/// <summary>
/// 内存目录内容实现
/// </summary>
internal class InMemoryDirectoryContents : IDirectoryContents
{
    private readonly IEnumerable<IFileInfo> _entries;

    /// <summary>
    /// 初始化内存目录内容
    /// </summary>
    /// <param name="entries">目录条目</param>
    public InMemoryDirectoryContents(IEnumerable<IFileInfo> entries)
    {
        _entries = entries;
    }

    /// <summary>
    /// 获取目录是否存在
    /// </summary>
    public bool Exists => true;

    /// <summary>
    /// 获取目录内容的枚举器
    /// </summary>
    /// <returns>文件信息枚举器</returns>
    public IEnumerator<IFileInfo> GetEnumerator()
    {
        return _entries.GetEnumerator();
    }

    /// <summary>
    /// 获取目录内容的枚举器
    /// </summary>
    /// <returns>枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 变更令牌实现
/// </summary>
internal class ChangeToken : IChangeToken
{
    private readonly List<Action<string>> _watchers;

    /// <summary>
    /// 初始化变更令牌
    /// </summary>
    /// <param name="watchers">监视器列表</param>
    public ChangeToken(List<Action<string>> watchers)
    {
        _watchers = watchers;
    }

    /// <summary>
    /// 获取是否已发生变化
    /// </summary>
    public bool HasChanged => false;

    /// <summary>
    /// 获取是否支持主动回调
    /// </summary>
    public bool ActiveChangeCallbacks => true;

    /// <summary>
    /// 注册变更回调
    /// </summary>
    /// <param name="callback">回调方法</param>
    /// <param name="state">状态对象</param>
    /// <returns>可释放的回调注册</returns>
    public IDisposable RegisterChangeCallback(Action<object> callback, object state)
    {
        _watchers.Add(_ => callback(state));
        return NullDisposable.Instance;
    }
}
