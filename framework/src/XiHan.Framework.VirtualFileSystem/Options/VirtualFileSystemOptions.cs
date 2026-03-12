#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOptions
// Guid:4d0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 05:40:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using Microsoft.Extensions.FileProviders;
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
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:VirtualFileSystem";

    /// <summary>
    /// 文件提供程序集合
    /// </summary>
    public List<PrioritizedFileProvider> Providers { get; } = [];

    /// <summary>
    /// 文件变更事件防抖毫秒数
    /// </summary>
    public int ChangeDebounceMilliseconds { get; set; } = 500;

    /// <summary>
    /// 是否启用文件变更追踪
    /// </summary>
    public bool EnableChangeTracking { get; set; } = true;

    /// <summary>
    /// 添加自定义文件提供程序
    /// </summary>
    /// <param name="provider">文件提供程序</param>
    /// <param name="priority">优先级</param>
    /// <returns></returns>
    public VirtualFileSystemOptions AddProvider(IFileProvider provider, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var key = GetProviderKey(provider);
        var existing = Providers.FirstOrDefault(x => GetProviderKey(x.Provider) == key);
        if (existing != null)
        {
            Providers.Remove(existing);
        }

        Providers.Add(new PrioritizedFileProvider(provider, priority));
        return this;
    }

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

        return AddProvider(new VirtualPhysicalFileProvider(fullPath, priority), priority);
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
        return AddProvider(new VirtualEmbeddedFileProvider(assembly, priority), priority);
    }

    /// <summary>
    /// 添加嵌入式资源提供程序
    /// </summary>
    /// <param name="assembly">资源所在程序集</param>
    /// <param name="priority">提供程序优先级</param>
    /// <returns>配置选项实例</returns>
    public VirtualFileSystemOptions AddEmbedded(Assembly assembly, int priority = 50)
    {
        return AddProvider(new VirtualEmbeddedFileProvider(assembly, priority), priority);
    }

    private static string GetProviderKey(IFileProvider provider)
    {
        return provider switch
        {
            VirtualPhysicalFileProvider physicalProvider => $"physical:{NormalizePath(physicalProvider.Root)}",
            VirtualEmbeddedFileProvider embeddedProvider => $"embedded:{embeddedProvider.Assembly.FullName}",
            _ => $"instance:{provider.GetType().FullName}:{provider.GetHashCode()}"
        };
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
    }
}
