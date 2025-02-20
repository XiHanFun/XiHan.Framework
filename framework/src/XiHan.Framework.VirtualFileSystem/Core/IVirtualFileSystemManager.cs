#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFileSystemManager
// Guid:d324bb09-40c2-4b58-b8c5-56f14ce08f5f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:24:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件系统管理器接口
/// </summary>
public interface IVirtualFileSystemManager
{
    /// <summary>
    /// 获取文件提供器
    /// </summary>
    /// <returns></returns>
    IFileProvider GetFileProvider();
}
