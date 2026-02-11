#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileMetadata
// Guid:e5f6a7b8-c9d0-4b1e-f2a3-b4c5d6e7f8a9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:08:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
