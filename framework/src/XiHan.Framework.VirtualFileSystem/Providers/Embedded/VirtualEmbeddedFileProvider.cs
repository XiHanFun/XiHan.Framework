// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using System.Reflection;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.VirtualFileSystem.Providers.Embedded;

/// <summary>
/// 带优先级的嵌入式文件提供程序
/// </summary>
public class VirtualEmbeddedFileProvider : EmbeddedFileProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public VirtualEmbeddedFileProvider(Assembly assembly, int priority = 50)
        : base(assembly)
    {
        Guard.NotNull(assembly, nameof(assembly));

        Assembly = assembly;
        Priority = priority;
    }

    /// <summary>
    /// 提供程序优先级
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// 程序集
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// 获取带优先级的文件信息
    /// </summary>
    public new PrioritizedFileInfo GetFileInfo(string subpath)
    {
        var fileInfo = base.GetFileInfo(subpath);
        return new PrioritizedFileInfo(fileInfo, Priority);
    }
}
