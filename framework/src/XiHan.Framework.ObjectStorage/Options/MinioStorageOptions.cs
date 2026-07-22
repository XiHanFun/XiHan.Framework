// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// MinIO配置
/// </summary>
public class MinioStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage:Minio";

    /// <summary>
    /// 终端节点
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 访问密钥
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// 密钥
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 默认存储桶
    /// </summary>
    public string DefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用SSL
    /// </summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>
    /// 区域
    /// </summary>
    public string? Region { get; set; }
}
