#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualCompositeFileProvider
// Guid:7d0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 06:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Composite;
using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.VirtualFileSystem.Providers.Composite;

/// <summary>
/// 带优先级的组合文件提供程序
/// </summary>
public class VirtualCompositeFileProvider : IFileProvider
{
    private readonly List<PrioritizedFileProvider> _providers;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="providers"></param>
    public VirtualCompositeFileProvider(IEnumerable<PrioritizedFileProvider> providers)
    {
        _providers = [.. providers.OrderByDescending(p => p.Priority)];
    }

    /// <summary>
    /// 获取目录内容
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var contents = new CompositeDirectoryContents([.. _providers.Select(p => p.Provider)], subpath);
        return new PrioritizedDirectoryContents(contents, _providers);
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        foreach (var provider in _providers)
        {
            var fileInfo = provider.Provider.GetFileInfo(subpath);
            if (fileInfo.Exists)
            {
                return new PrioritizedFileInfo(fileInfo, provider.Priority);
            }
        }
        return new NotFoundFileInfo(subpath);
    }

    /// <summary>
    /// 监视
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    public IChangeToken Watch(string filter)
    {
        return new CompositeChangeToken([.. _providers.Select(p => p.Provider.Watch(filter))]);
    }
}
