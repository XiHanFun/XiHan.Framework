// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// OpenID Connect 发现文档
/// </summary>
/// <remarks>
/// 字段名由 OIDC Discovery 1.0 规定，为 snake_case，故显式标注 JsonPropertyName，不随全局序列化策略变化。
/// 标准客户端库据此自动完成端点与验签配置。
/// </remarks>
public sealed class OidcDiscoveryDocument
{
    /// <summary>签发者标识</summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>授权端点</summary>
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    /// <summary>令牌端点</summary>
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>用户信息端点</summary>
    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; set; } = string.Empty;

    /// <summary>公钥集地址</summary>
    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    /// <summary>令牌撤销端点</summary>
    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; set; } = string.Empty;

    /// <summary>支持的 scope</summary>
    [JsonPropertyName("scopes_supported")]
    public IReadOnlyCollection<string> ScopesSupported { get; set; } = [];

    /// <summary>支持的响应类型</summary>
    [JsonPropertyName("response_types_supported")]
    public IReadOnlyCollection<string> ResponseTypesSupported { get; set; } = ["code"];

    /// <summary>支持的响应模式</summary>
    [JsonPropertyName("response_modes_supported")]
    public IReadOnlyCollection<string> ResponseModesSupported { get; set; } = ["query"];

    /// <summary>支持的授权类型</summary>
    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyCollection<string> GrantTypesSupported { get; set; } =
        ["authorization_code", "refresh_token", "client_credentials"];

    /// <summary>主体标识类型</summary>
    [JsonPropertyName("subject_types_supported")]
    public IReadOnlyCollection<string> SubjectTypesSupported { get; set; } = ["public"];

    /// <summary>id_token 签名算法</summary>
    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported { get; set; } = [SecurityAlgorithms.RsaSha256];

    /// <summary>客户端认证方式</summary>
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public IReadOnlyCollection<string> TokenEndpointAuthMethodsSupported { get; set; } =
        ["client_secret_basic", "client_secret_post", "none"];

    /// <summary>PKCE 挑战方式</summary>
    [JsonPropertyName("code_challenge_methods_supported")]
    public IReadOnlyCollection<string> CodeChallengeMethodsSupported { get; set; } = ["S256", "plain"];

    /// <summary>可能出现的声明</summary>
    [JsonPropertyName("claims_supported")]
    public IReadOnlyCollection<string> ClaimsSupported { get; set; } = [];

    /// <summary>
    /// 按签发者根地址构造发现文档，端点路径取协议标准值。
    /// </summary>
    /// <param name="options">OIDC 配置</param>
    public static OidcDiscoveryDocument Create(OidcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var issuer = options.Issuer.TrimEnd('/');

        return new OidcDiscoveryDocument
        {
            Issuer = issuer,
            AuthorizationEndpoint = issuer + OidcConstants.Endpoints.Authorize,
            TokenEndpoint = issuer + OidcConstants.Endpoints.Token,
            UserInfoEndpoint = issuer + OidcConstants.Endpoints.UserInfo,
            JwksUri = issuer + OidcConstants.Endpoints.Jwks,
            RevocationEndpoint = issuer + OidcConstants.Endpoints.Revocation,
            ScopesSupported = options.ScopesSupported,
            ClaimsSupported =
            [
                OidcConstants.Claims.Subject,
                OidcConstants.Claims.Nonce,
                OidcConstants.Claims.AuthTime,
                OidcConstants.Claims.PreferredUserName,
                OidcConstants.Claims.Name,
                OidcConstants.Claims.Picture,
                OidcConstants.Claims.Email,
                OidcConstants.Claims.EmailVerified,
                OidcConstants.Claims.PhoneNumber,
                OidcConstants.Claims.PhoneNumberVerified,
                OidcConstants.Claims.UpdatedAt
            ]
        };
    }
}
