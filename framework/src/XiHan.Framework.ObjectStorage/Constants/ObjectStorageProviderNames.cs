#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ObjectStorageProviderNames
// Guid:efb59f4f-1925-4384-95de-177c32186593
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectStorage.Constants;

/// <summary>
/// 对象存储提供程序名称常量
/// </summary>
public static class ObjectStorageProviderNames
{
    /// <summary>
    /// 本地存储
    /// </summary>
    public const string Local = "Local";

    /// <summary>
    /// MinIO 存储
    /// </summary>
    public const string Minio = "MinIO";

    /// <summary>
    /// 阿里云 OSS
    /// </summary>
    public const string AliyunOss = "AliyunOSS";

    /// <summary>
    /// 腾讯云 COS
    /// </summary>
    public const string TencentCos = "TencentCOS";
}
