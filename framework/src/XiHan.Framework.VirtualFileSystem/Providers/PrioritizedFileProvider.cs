// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Providers;

/// <summary>
/// 带优先级的文件提供程序封装
/// </summary>
public class PrioritizedFileProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <param name="priority">优先级</param>
    public PrioritizedFileProvider(IFileProvider provider, int priority)
    {
        Provider = provider;
        Priority = priority;
    }

    /// <summary>
    /// 文件提供程序实例
    /// </summary>
    public IFileProvider Provider { get; }

    /// <summary>
    /// 优先级(数值越大优先级越高)
    /// </summary>
    public int Priority { get; }
}
