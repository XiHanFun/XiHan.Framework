#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MinioStorageOptions
// Guid:c9d0e1f2-a3b4-4f5c-d6e7-f8a9b0c1d2e3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/10 10:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.VirtualFileSystem.Storage.Options;

/// <summary>
/// MinIO配置
/// </summary>
public class MinioStorageOptions
{
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
