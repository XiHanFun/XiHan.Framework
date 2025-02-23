#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumerableDirectoryContents
// Guid:aa0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 7:45:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;
using System.Collections;

namespace XiHan.Framework.VirtualFileSystem.Providers;

/// <summary>
/// 可枚举目录内容包装器
/// </summary>
public class EnumerableDirectoryContents : IDirectoryContents
{
    private readonly IEnumerable<IFileInfo> _files;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="files"></param>
    public EnumerableDirectoryContents(IEnumerable<IFileInfo> files)
    {
        _files = files;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists => true;

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IFileInfo> GetEnumerator()
    {
        return _files.GetEnumerator();
    }

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
