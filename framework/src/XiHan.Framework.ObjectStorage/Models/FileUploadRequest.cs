// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Models;

/// <summary>
/// 文件上传请求
/// </summary>
public class FileUploadRequest
{
    /// <summary>
    /// 文件流
    /// </summary>
    public Stream FileStream { get; set; } = Stream.Null;

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

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
