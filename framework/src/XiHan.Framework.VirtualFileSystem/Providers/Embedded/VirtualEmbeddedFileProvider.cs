#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualEmbeddedFileProvider
// Guid:6d0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 6:30:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System.Reflection;
using XiHan.Framework.Utils.System;

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
        _ = Guard.NotNull(assembly, nameof(assembly));

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
