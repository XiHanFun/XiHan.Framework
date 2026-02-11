#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AliyunOssStorageOptions
// Guid:c9d0e1f2-a3b4-4f5c-d6e7-f8a9b0c1d2e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/10 10:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 阿里云OSS配置
/// </summary>
public class AliyunOssStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage:AliyunOss";

    /// <summary>
    /// 访问密钥ID
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// 访问密钥Secret
    /// </summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>
    /// 终端节点
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 默认存储桶
    /// </summary>
    public string DefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// CDN域名
    /// </summary>
    public string? CdnDomain { get; set; }

    /// <summary>
    /// 是否使用内网
    /// </summary>
    public bool UseInternal { get; set; } = false;
}
