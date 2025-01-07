#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFileSystemOperations
// Guid:4adb0d85-0aa1-447e-9fcf-fd999dd52c03
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/1/7 6:34:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Core;

/// <summary>
/// 虚拟文件系统操作接口
/// </summary>
public interface IVirtualFileSystemOperations
{
    /// <summary>
    /// 文件是否存在
    /// </summary>
    bool FileExists(string filePath);

    /// <summary>
    /// 获取文件信息
    /// </summary>
    IFileInfo GetFileInfo(string filePath);

    /// <summary>
    /// 获取目录内容
    /// </summary>
    IDirectoryContents GetDirectoryContents(string path);

    /// <summary>
    /// 读取文件内容
    /// </summary>
    Task<string> ReadFileAsync(string filePath);

    /// <summary>
    /// 获取所有文件
    /// </summary>
    IEnumerable<IFileInfo> GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
}
