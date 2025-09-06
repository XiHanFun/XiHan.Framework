#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOptions
// Guid:4d0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 5:40:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.VirtualFileSystem.Providers;
using XiHan.Framework.VirtualFileSystem.Providers.Embedded;
using XiHan.Framework.VirtualFileSystem.Providers.Physical;

namespace XiHan.Framework.VirtualFileSystem.Options;

/// <summary>
/// 虚拟文件系统配置选项
/// </summary>
public class VirtualFileSystemOptions
{
    /// <summary>
    /// 文件提供程序集合
    /// </summary>
    public List<PrioritizedFileProvider> Providers { get; } = [];

    /// <summary>
    /// 添加物理文件提供程序
    /// </summary>
    /// <param name="rootPath">物理根路径</param>
    /// <param name="priority">提供程序优先级</param>
    /// <returns>配置选项实例</returns>
    public VirtualFileSystemOptions AddPhysical(string rootPath, int priority = 100)
    {
        var fullPath = Path.GetFullPath(rootPath);

        // 自动创建不存在的目录
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        var provider = new VirtualPhysicalFileProvider(fullPath, priority);
        Providers.Add(new PrioritizedFileProvider(provider, priority));
        return this;
    }

    /// <summary>
    /// 添加嵌入式资源提供程序
    /// </summary>
    /// <typeparam name="TAssembly">资源所在程序集类型</typeparam>
    /// <param name="priority">提供程序优先级</param>
    /// <returns>配置选项实例</returns>
    public VirtualFileSystemOptions AddEmbedded<TAssembly>(int priority = 50)
    {
        var assembly = typeof(TAssembly).Assembly;
        var provider = new VirtualEmbeddedFileProvider(assembly, priority);
        Providers.Add(new PrioritizedFileProvider(provider, priority));
        return this;
    }

    /// <summary>
    /// 添加嵌入式资源提供程序
    /// </summary>
    /// <param name="assembly">资源所在程序集</param>
    /// <param name="priority">提供程序优先级</param>
    /// <returns>配置选项实例</returns>
    public VirtualFileSystemOptions AddEmbedded(Assembly assembly, int priority = 50)
    {
        var provider = new VirtualEmbeddedFileProvider(assembly, priority);
        Providers.Add(new PrioritizedFileProvider(provider, priority));
        return this;
    }
}
