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
        // 用 f.Name（裸文件名）反查来源 provider 并不总能命中（provider 多按相对路径索引，子目录/合并视图下
        // GetFileInfo(f.Name) 可能全 false）。原 First 在无匹配时抛 "Sequence contains no matching element"，
        // 致整个目录枚举（含 JSON 本地化资源加载）崩溃。改 FirstOrDefault：无匹配时按默认优先级 0 包装，
        // 文件仍正常枚举（该文件确在 _inner 中、本就存在），仅退化优先级信息、不再抛。
        return _inner
            .Select(f => new PrioritizedFileInfo(
                f,
                _providers.FirstOrDefault(p => p.Provider.GetFileInfo(f.Name).Exists)?.Priority ?? 0))
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
