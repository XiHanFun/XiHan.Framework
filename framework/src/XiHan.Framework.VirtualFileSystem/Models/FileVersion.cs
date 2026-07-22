// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.VirtualFileSystem.Models;

/// <summary>
/// 文件版本信息
/// </summary>
public class FileVersion
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public FileVersion(IFileInfo file)
    {
        using var stream = file.CreateReadStream();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        Content = memoryStream.ToArray();
        ContentHash = HashHelper.ByteHash(Content);
        Length = file.Length;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 文件内容哈希
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// 版本时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 文件内容
    /// </summary>
    public byte[] Content { get; }
}
