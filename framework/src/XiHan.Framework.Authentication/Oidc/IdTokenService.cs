// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// id_token 签发请求
/// </summary>
/// <param name="Subject">用户主体标识</param>
/// <param name="Audience">客户端标识（client_id）</param>
/// <param name="Nonce">授权请求带入的 nonce，原样回填；无则不写入</param>
/// <param name="AuthenticationTime">本次认证发生时刻，写入 auth_time</param>
/// <param name="AccessToken">同批签发的访问令牌，用于计算 at_hash；无则不写入</param>
/// <param name="AdditionalClaims">按已授予 scope 解析出的资料声明</param>
public sealed record IdTokenRequest(
    string Subject,
    string Audience,
    string? Nonce = null,
    DateTimeOffset? AuthenticationTime = null,
    string? AccessToken = null,
    IReadOnlyCollection<Claim>? AdditionalClaims = null);

/// <summary>
/// id_token 签发服务
/// </summary>
public interface IIdTokenService
{
    /// <summary>
    /// 签发 id_token
    /// </summary>
    string Issue(IdTokenRequest request);
}

/// <summary>
/// 基于 RS256 的 id_token 签发服务
/// </summary>
public sealed class IdTokenService : IIdTokenService
{
    private readonly IOidcSigningKeyProvider _signingKeyProvider;
    private readonly OidcOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public IdTokenService(IOidcSigningKeyProvider signingKeyProvider, IOptions<OidcOptions> options)
    {
        _signingKeyProvider = signingKeyProvider;
        _options = options.Value;
    }

    /// <summary>
    /// 按请求组装 sub、jti、nonce、auth_time、at_hash 及资料声明，以 RS256 签发 id_token
    /// </summary>
    /// <param name="request">id_token 签发请求</param>
    /// <returns>序列化后的 id_token 字符串</returns>
    public string Issue(IdTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Audience);

        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(OidcConstants.Claims.Subject, request.Subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrWhiteSpace(request.Nonce))
        {
            claims.Add(new Claim(OidcConstants.Claims.Nonce, request.Nonce));
        }

        if (request.AuthenticationTime is { } authTime)
        {
            claims.Add(new Claim(
                OidcConstants.Claims.AuthTime,
                authTime.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64));
        }

        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            claims.Add(new Claim(OidcConstants.Claims.AccessTokenHash, ComputeAccessTokenHash(request.AccessToken)));
        }

        if (request.AdditionalClaims is { Count: > 0 })
        {
            claims.AddRange(request.AdditionalClaims);
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: request.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_options.IdTokenExpirationMinutes).UtcDateTime,
            signingCredentials: _signingKeyProvider.GetSigningCredentials());

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// at_hash：对访问令牌取 SHA-256，取前半段做 base64url。
    /// </summary>
    /// <remarks>取前半段是 OIDC Core 1.0 对 RS256 的规定（哈希长度的左半部分）。</remarks>
    private static string ComputeAccessTokenHash(string accessToken)
    {
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(accessToken));
        return Base64UrlEncoder.Encode(hash.AsSpan(0, hash.Length / 2).ToArray());
    }
}
