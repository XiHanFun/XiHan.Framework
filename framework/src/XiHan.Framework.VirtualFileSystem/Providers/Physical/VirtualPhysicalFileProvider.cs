// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.VirtualFileSystem.Providers.Physical;

/// <summary>
/// 带优先级的物理文件提供程序
/// </summary>
public class VirtualPhysicalFileProvider : PhysicalFileProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public VirtualPhysicalFileProvider(string root, int priority = 100) : base(root)
    {
        Guard.NotNull(root, nameof(root));

        if (!Path.IsPathRooted(root))
        {
            throw new ArgumentException("必须提供绝对路径", nameof(root));
        }

        Priority = priority;
    }

    /// <summary>
    /// 提供程序优先级
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// 获取带优先级的文件信息
    /// </summary>
    public new PrioritizedFileInfo GetFileInfo(string subpath)
    {
        var fileInfo = base.GetFileInfo(subpath);
        return new PrioritizedFileInfo(fileInfo, Priority);
    }
}
