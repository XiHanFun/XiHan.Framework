#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright 2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFile
// Guid:89b884f0-0837-41b4-af4a-99d7a621fcb7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:12:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件接口
/// </summary>
/// <remarks>
/// 定义了虚拟文件的基本属性和操作方法，继承自 IFileInfo 以支持标准文件操作
/// </remarks>
public interface IVirtualFile : IFileInfo
{
    /// <summary>
    /// 异步创建文件读取流
    /// </summary>
    /// <returns></returns>
    Task<Stream> CreateReadStreamAsync();
}
