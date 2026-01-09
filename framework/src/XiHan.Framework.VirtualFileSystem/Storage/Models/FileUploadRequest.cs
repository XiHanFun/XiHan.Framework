#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileUploadRequest
// Guid:b2c3d4e5-f6a7-48b9-c0d1-e2f3a4b5c6d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Storage.Models;

/// <summary>
/// 文件上传请求
/// </summary>
public class FileUploadRequest
{
    /// <summary>
    /// 文件流
    /// </summary>
    public required Stream FileStream { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// 存储路径
    /// </summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// 内容类型（MIME Type）
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// 是否覆盖已存在的文件
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// 访问权限（private、public-read等）
    /// </summary>
    public string AccessControl { get; set; } = "private";

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// 缓存控制
    /// </summary>
    public string? CacheControl { get; set; }

    /// <summary>
    /// 上传进度回调
    /// </summary>
    public Action<long, long>? ProgressCallback { get; set; }
}
