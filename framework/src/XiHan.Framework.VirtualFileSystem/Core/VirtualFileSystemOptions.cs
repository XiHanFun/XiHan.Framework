#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOptions
// Guid:f6f1c67e-cb76-467e-b5ef-f29e5a112264
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:16:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件系统选项
/// </summary>
public class VirtualFileSystemOptions
{
    /// <summary>
    /// 嵌入式程序集列表
    /// </summary>
    public List<Assembly> EmbeddedAssemblies { get; } = [];

    /// <summary>
    /// 物理文件路径列表
    /// </summary>
    public List<string> PhysicalPaths { get; } = [];

    private readonly List<IFileProvider> _fileProviders;

    /// <summary>
    /// 文件提供器列表
    /// </summary>
    public IReadOnlyList<IFileProvider> FileProviders => _fileProviders;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VirtualFileSystemOptions()
    {
        _fileProviders = [];
    }

    /// <summary>
    /// 添加嵌入式程序集
    /// </summary>
    /// <param name="assembly"></param>
    public void AddEmbeddedAssembly(Assembly assembly)
    {
        EmbeddedAssemblies.Add(assembly);
    }

    /// <summary>
    /// 添加物理文件路径
    /// </summary>
    /// <param name="path"></param>
    public void AddPhysicalPath(string path)
    {
        PhysicalPaths.Add(path);
    }

    /// <summary>
    /// 添加文件提供器
    /// </summary>
    /// <param name="fileProvider">文件提供器</param>
    public void AddFileProvider(IFileProvider fileProvider)
    {
        _fileProviders.Add(fileProvider);
    }
}
