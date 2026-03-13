#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OpenApiSecurityClient
// Guid:87c9c1f3-14b4-4d42-8db2-0a6374d7fd39
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:32:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 客户端安全模型
/// </summary>
public class OpenApiSecurityClient
{
    /// <summary>
    /// AccessKey
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// SecretKey
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 请求体加密密钥（为空时回退 SecretKey）
    /// </summary>
    public string? EncryptKey { get; set; }

    /// <summary>
    /// RSA 公钥
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// SM2 公钥
    /// </summary>
    public string? Sm2PublicKey { get; set; }

    /// <summary>
    /// 客户端签名算法（为空时使用全局默认）
    /// </summary>
    public string? SignatureAlgorithm { get; set; }

    /// <summary>
    /// 客户端内容签名算法（为空时使用全局默认）
    /// </summary>
    public string? ContentSignatureAlgorithm { get; set; }

    /// <summary>
    /// 客户端加密算法（为空时使用全局默认）
    /// </summary>
    public string? EncryptionAlgorithm { get; set; }

    /// <summary>
    /// 是否允许响应加密
    /// </summary>
    public bool AllowResponseEncryption { get; set; } = true;

    /// <summary>
    /// IP 白名单
    /// </summary>
    public IReadOnlyCollection<string> IpWhitelist { get; set; } = [];

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
