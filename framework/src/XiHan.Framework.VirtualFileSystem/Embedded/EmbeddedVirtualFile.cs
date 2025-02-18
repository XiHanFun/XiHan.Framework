#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmbeddedVirtualFile
// Guid:bcb8ce5a-ee71-4214-b10e-570089ea5c07
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/8 6:00:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.VirtualFileSystem.Core;

namespace XiHan.Framework.VirtualFileSystem.Embedded;

/// <summary>
/// 嵌入式虚拟文件
/// </summary>
internal class EmbeddedVirtualFile : IVirtualFile
{
    private readonly Stream _stream;

    /// <summary>
    /// 初始化嵌入式虚拟文件
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="stream">文件流</param>
    /// <param name="resourcePath">资源路径</param>
    public EmbeddedVirtualFile(Stream stream, string path, string resourcePath)
    {
        _stream = stream;

        Name = Path.GetFileName(path);
        PhysicalPath = null;
        Length = stream.Length;
        LastModified = DateTimeOffset.UtcNow;
        IsDirectory = false;
        Exists = true;
    }

    /// <summary>
    /// 获取文件名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取文件大小（字节）
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// 获取最后修改时间
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    /// 获取是否是目录
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// 获取文件是否存在
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// 获取物理文件路径
    /// </summary>
    public string? PhysicalPath { get; }

    /// <summary>
    /// 创建文件读取流
    /// </summary>
    /// <returns>文件流</returns>
    public Stream CreateReadStream()
    {
        return _stream;
    }

    /// <summary>
    /// 异步创建文件读取流
    /// </summary>
    /// <returns>文件流任务</returns>
    public Task<Stream> CreateReadStreamAsync()
    {
        return Task.FromResult(_stream);
    }
}
