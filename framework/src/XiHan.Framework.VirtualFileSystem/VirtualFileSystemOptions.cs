#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileSystemOptions
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:20:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem;

/// <summary>
/// 虚拟文件系统选项
/// </summary>
public class VirtualFileSystemOptions
{
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
    /// 添加文件提供器
    /// </summary>
    /// <param name="fileProvider">文件提供器</param>
    public void AddFileProvider(IFileProvider fileProvider)
    {
        _fileProviders.Add(fileProvider);
    }
}
