#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MemoryFileInfo
// Guid:8a0a0a0a-0a0a-0a0a-0a0a-0a0a0a0a0a0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 7:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.FileProviders;

namespace XiHan.Framework.VirtualFileSystem.Providers.Memory;

/// <summary>
/// 内存文件信息
/// </summary>
internal class MemoryFileInfo : IFileInfo
{
    private readonly MemoryFile _file;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="file"></param>
    public MemoryFileInfo(MemoryFile file)
    {
        _file = file;
    }

    /// <summary>
    /// 是否存在
    /// </summary>
    public bool Exists => true;

    /// <summary>
    /// 长度
    /// </summary>
    public long Length => _file.Content.Length;

    /// <summary>
    /// 路径
    /// </summary>
    public string PhysicalPath => null!;

    /// <summary>
    /// 名称
    /// </summary>
    public string Name => Path.GetFileName(_file.Path);

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastModified => _file.LastModified;

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory => false;

    /// <summary>
    /// 创建读取流
    /// </summary>
    /// <returns></returns>
    public Stream CreateReadStream()
    {
        return new MemoryStream(_file.Content);
    }
}
