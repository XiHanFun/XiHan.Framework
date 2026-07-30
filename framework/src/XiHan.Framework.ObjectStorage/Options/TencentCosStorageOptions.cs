// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 腾讯云COS配置
/// </summary>
public class TencentCosStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage:TencentCos";

    /// <summary>
    /// 密钥ID
    /// </summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>
    /// 密钥Key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 地域
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 默认存储桶
    /// </summary>
    public string DefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// CDN域名
    /// </summary>
    public string? CdnDomain { get; set; }
}
