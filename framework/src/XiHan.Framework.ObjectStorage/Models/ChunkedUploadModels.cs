// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Models;

/// <summary>
/// 分片上传初始化请求
/// </summary>
public class ChunkedUploadInitRequest
{
    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件总大小
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 分片大小
    /// </summary>
    public int ChunkSize { get; set; } = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// 内容类型
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// 访问权限
    /// </summary>
    public string AccessControl { get; set; } = "private";

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// 分片上传请求
/// </summary>
public class ChunkUploadRequest
{
    /// <summary>
    /// 上传ID
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 分片序号（从1开始）
    /// </summary>
    public int ChunkNumber { get; set; }

    /// <summary>
    /// 分片数据
    /// </summary>
    public Stream ChunkData { get; set; } = Stream.Null;

    /// <summary>
    /// 分片大小
    /// </summary>
    public long ChunkSize { get; set; }

    /// <summary>
    /// 文件总大小
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 总分片数
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// 分片MD5（可选，用于校验）
    /// </summary>
    public string? ChunkMd5 { get; set; }

    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string? BucketName { get; set; }
}

/// <summary>
/// 分片上传结果
/// </summary>
public class ChunkUploadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 分片序号
    /// </summary>
    public int ChunkNumber { get; set; }

    /// <summary>
    /// 分片ETag（用于完成上传时的校验）
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 分片上传完成请求
/// </summary>
public class ChunkedUploadCompleteRequest
{
    /// <summary>
    /// 上传ID
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 分片列表（按序号排序的ETag列表）
    /// </summary>
    public List<ChunkInfo> ChunkInfos { get; set; } = [];

    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string? BucketName { get; set; }
}

/// <summary>
/// 分片信息
/// </summary>
public class ChunkInfo
{
    /// <summary>
    /// 分片序号
    /// </summary>
    public int ChunkNumber { get; set; }

    /// <summary>
    /// 分片ETag
    /// </summary>
    public string? ETag { get; set; }
}
