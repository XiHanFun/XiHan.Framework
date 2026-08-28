// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Collections;

namespace XiHan.Framework.VirtualFileSystem.Tests.TestSupport;

/// <summary>
/// 手写的目录内容替身
/// </summary>
internal sealed class FakeDirectoryContents : IDirectoryContents
{
    private readonly IReadOnlyList<IFileInfo> _files;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="exists">目录是否存在</param>
    /// <param name="files">目录内条目</param>
    public FakeDirectoryContents(bool exists, params IFileInfo[] files)
    {
        Exists = exists;
        _files = files;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns>条目枚举器</returns>
    public IEnumerator<IFileInfo> GetEnumerator()
    {
        return _files.GetEnumerator();
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns>条目枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
