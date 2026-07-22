// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Models;

/// <summary>
/// 文件元数据
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// 文件名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// ETag
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 是否是目录
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// 访问URL
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
