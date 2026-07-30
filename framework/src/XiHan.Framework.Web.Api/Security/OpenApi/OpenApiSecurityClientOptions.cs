// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// OpenApi 客户端配置项
/// </summary>
public class OpenApiSecurityClientOptions
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
    /// RSA 公钥（当签名算法为 RSASHA256 时使用）
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    /// SM2 公钥（当签名算法为 SM2 时使用）
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
    public List<string> IpWhitelist { get; set; } = [];

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
