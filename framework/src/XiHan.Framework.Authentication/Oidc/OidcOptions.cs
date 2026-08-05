// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// OpenID Connect 配置
/// </summary>
/// <remarks>
/// id_token 由第三方验签，因此必须用非对称密钥签名（RS256），不能复用
/// <c>XiHan:Authentication:Jwt</c> 的对称 SecretKey —— 把对称密钥交出去等于把签发能力交出去。
/// </remarks>
public class OidcOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:Authentication:Oidc";

    /// <summary>
    /// 是否启用。关闭时不注册发现文档、JWKS 与 id_token 签发。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 签发者标识，须与对外可访问的根地址完全一致（含协议与端口，结尾不带斜杠）。
    /// 客户端会拿它与 id_token 的 iss 逐字符比对。
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// RSA 私钥（PKCS#8 PEM）。留空时改从 <see cref="SigningKeyPath"/> 读取。
    /// </summary>
    public string? SigningKeyPem { get; set; }

    /// <summary>
    /// RSA 私钥文件路径。文件不存在且 <see cref="AutoGenerateSigningKey"/> 为真时自动生成并写入。
    /// 相对路径相对于应用基目录。
    /// </summary>
    public string SigningKeyPath { get; set; } = "keys/oidc-signing.pem";

    /// <summary>
    /// 密钥缺失时是否自动生成。多实例部署必须关闭并预置同一把密钥，
    /// 否则各实例签出的 id_token 互不认可。
    /// </summary>
    public bool AutoGenerateSigningKey { get; set; } = true;

    /// <summary>
    /// 密钥标识。留空则由公钥指纹推导，轮换密钥时随之改变。
    /// </summary>
    public string? KeyId { get; set; }

    /// <summary>
    /// id_token 有效期（分钟）
    /// </summary>
    public int IdTokenExpirationMinutes { get; set; } = 10;

    /// <summary>
    /// 对外声明支持的 scope
    /// </summary>
    public List<string> ScopesSupported { get; set; } =
    [
        OidcConstants.Scopes.OpenId,
        OidcConstants.Scopes.Profile,
        OidcConstants.Scopes.Email,
        OidcConstants.Scopes.Phone,
        OidcConstants.Scopes.OfflineAccess
    ];
}
