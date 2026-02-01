#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PrioritizedDirectoryContents
// Guid:5caced57-142a-4c16-8b35-d4a8c991ac3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 06:22:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System.Collections;

namespace XiHan.Framework.VirtualFileSystem.Providers;

/// <summary>
/// 优先级目录内容包装器
/// </summary>
public class PrioritizedDirectoryContents : IDirectoryContents
{
    private readonly IDirectoryContents _inner;
    private readonly List<PrioritizedFileProvider> _providers;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="providers"></param>
    public PrioritizedDirectoryContents(IDirectoryContents inner, List<PrioritizedFileProvider> providers)
    {
        _inner = inner;
        _providers = providers;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists => _inner.Exists;

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    IEnumerator<IFileInfo> IEnumerable<IFileInfo>.GetEnumerator()
    {
        return _inner
            .Select(f => new PrioritizedFileInfo(f, _providers.First(p => p.Provider.GetFileInfo(f.Name).Exists).Priority))
            .GetEnumerator();
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<IFileInfo>)this).GetEnumerator();
    }
}
