#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TencentCosStorageOptions
// Guid:c9d0e1f2-a3b4-4f5c-d6e7-f8a9b0c1d2e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
