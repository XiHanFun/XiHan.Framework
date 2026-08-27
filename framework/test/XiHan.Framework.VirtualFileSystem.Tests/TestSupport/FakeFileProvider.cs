// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.VirtualFileSystem.Tests;

/// <summary>
/// 手写的文件提供程序替身
/// </summary>
/// <remarks>
/// 只按精确子路径命中，用来验证组合提供器的优先级挑选与回退顺序，不掺入真实文件系统语义。
/// </remarks>
internal sealed class FakeFileProvider : IFileProvider
{
    private readonly Dictionary<string, IFileInfo> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// GetDirectoryContents 的返回值
    /// </summary>
    public IDirectoryContents DirectoryContents { get; set; } = new FakeDirectoryContents(false);

    /// <summary>
    /// Watch 返回的变更令牌
    /// </summary>
    public FakeChangeToken ChangeToken { get; } = new();

    /// <summary>
    /// 最近一次 Watch 收到的过滤条件
    /// </summary>
    public string? LastWatchFilter { get; private set; }

    /// <summary>
    /// 登记一个可命中的文件
    /// </summary>
    /// <param name="subpath">子路径</param>
    /// <param name="file">文件信息</param>
    /// <returns>自身，便于链式调用</returns>
    public FakeFileProvider WithFile(string subpath, IFileInfo file)
    {
        _files[subpath] = file;
        return this;
    }

    /// <summary>
    /// 设定目录内容
    /// </summary>
    /// <param name="exists">目录是否存在</param>
    /// <param name="files">目录内条目</param>
    /// <returns>自身，便于链式调用</returns>
    public FakeFileProvider WithDirectory(bool exists, params IFileInfo[] files)
    {
        DirectoryContents = new FakeDirectoryContents(exists, files);
        return this;
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath">子路径</param>
    /// <returns>文件信息</returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        return _files.TryGetValue(subpath, out var file) ? file : new NotFoundFileInfo(subpath);
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath">子路径</param>
    /// <returns>目录内容</returns>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return DirectoryContents;
    }

    /// <summary>
    /// 监视
    /// </summary>
    /// <param name="filter">过滤条件</param>
    /// <returns>变更令牌</returns>
    public IChangeToken Watch(string? filter)
    {
        LastWatchFilter = filter;
        return ChangeToken;
    }
}
