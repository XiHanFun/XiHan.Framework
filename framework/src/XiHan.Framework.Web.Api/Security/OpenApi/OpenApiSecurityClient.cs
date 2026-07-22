// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    /// <summary>
    /// 凭证归属用户标识（数据库凭证有值；配置型固定客户端无归属用户则为空）。
    /// 供开放接口日志记录"是谁的密钥发起的调用"，签名调用无 JWT 用户时以此作为审计主体。
    /// </summary>
    public long? OwnerUserId { get; set; }
}
