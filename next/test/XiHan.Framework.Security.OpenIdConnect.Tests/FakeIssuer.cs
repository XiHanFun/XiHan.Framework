// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace XiHan.Framework.Security.OpenIdConnect.Tests;

/// <summary>
/// 桩签发者。
/// </summary>
/// <remarks>
/// 以 HTTP 处理器身份应答发现文档与 JWKS，并用当前 RSA 密钥签发令牌；可轮换密钥、下线、改签发者。
/// </remarks>
internal sealed class FakeIssuer : HttpMessageHandler
{
    /// <summary>
    /// 签发者地址。
    /// </summary>
    public const string Authority = "https://issuer.test";

    /// <summary>
    /// 默认受众。
    /// </summary>
    public const string Audience = "api";

    /// <summary>
    /// JWKS 当前公开的密钥。
    /// </summary>
    /// <remarks>
    /// 最后一个是签发用的当前密钥。
    /// </remarks>
    private readonly List<RsaSecurityKey> _keys = [];

    /// <summary>
    /// 创建并生成第一把密钥。
    /// </summary>
    public FakeIssuer() => Rotate();

    /// <summary>
    /// 发现文档里的 <c>issuer</c>。
    /// </summary>
    public string Issuer { get; set; } = Authority;

    /// <summary>
    /// 为真时所有请求抛连接失败。
    /// </summary>
    public bool Offline { get; set; }

    /// <summary>
    /// 发现文档被请求的次数。
    /// </summary>
    public int MetadataRequests { get; private set; }

    /// <summary>
    /// JWKS 被请求的次数。
    /// </summary>
    public int JwksRequests { get; private set; }

    /// <summary>
    /// 当前签发密钥。
    /// </summary>
    public RsaSecurityKey CurrentKey => _keys[^1];

    /// <summary>
    /// 轮换：生成新密钥并只公开它（旧密钥从 JWKS 消失）。
    /// </summary>
    public RsaSecurityKey Rotate()
    {
        var key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = Guid.NewGuid().ToString("N") };
        _keys.Clear();
        _keys.Add(key);
        return key;
    }

    /// <summary>
    /// 签发 RS256 令牌。
    /// </summary>
    /// <remarks>
    /// 默认用当前密钥、主体 alice、签发者取发现文档的、十分钟有效期。
    /// </remarks>
    public string CreateToken(string? subject = "alice", RsaSecurityKey? key = null, string? issuer = null, string audience = Audience, TimeSpan? lifetime = null, params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity();
        if (subject is not null)
        {
            identity.AddClaim(new Claim("sub", subject));
        }

        foreach (var (type, value) in claims)
        {
            identity.AddClaim(new Claim(type, value));
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience,
            Subject = identity,
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10)),
            SigningCredentials = new SigningCredentials(key ?? CurrentKey, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("The issuer is offline.");
        }

        switch (request.RequestUri!.AbsolutePath)
        {
            case "/.well-known/openid-configuration":
                MetadataRequests++;
                return Task.FromResult(Json($$"""{"issuer":"{{Issuer}}","jwks_uri":"{{Authority}}/jwks"}"""));
            case "/jwks":
                JwksRequests++;
                return Task.FromResult(Json(Jwks()));
            default:
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var key in _keys)
            {
                key.Rsa.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// JSON 应答。
    /// </summary>
    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// 当前公开密钥的 JWKS 文档。
    /// </summary>
    private string Jwks()
    {
        var entries = _keys.Select(key =>
        {
            var parameters = key.Rsa.ExportParameters(false);
            return $$"""{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{key.KeyId}}","n":"{{Base64UrlEncoder.Encode(parameters.Modulus)}}","e":"{{Base64UrlEncoder.Encode(parameters.Exponent)}}"}""";
        });
        return $$"""{"keys":[{{string.Join(',', entries)}}]}""";
    }
}
