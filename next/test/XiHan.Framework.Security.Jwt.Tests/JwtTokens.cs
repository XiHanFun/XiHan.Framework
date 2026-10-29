// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// 测试用 HS256 令牌：共享的密钥、签发者与受众，以及按场景改各项的签发方法。
/// </summary>
internal static class JwtTokens
{
    /// <summary>
    /// 签名密钥（至少 32 字节）。
    /// </summary>
    public const string Secret = "unit-test-secret-key-with-at-least-32-bytes!";

    /// <summary>
    /// 签发者。
    /// </summary>
    public const string Issuer = "https://issuer.test";

    /// <summary>
    /// 受众。
    /// </summary>
    public const string Audience = "api";

    /// <summary>
    /// 签发令牌。
    /// </summary>
    /// <remarks>
    /// 默认主体 alice、十分钟有效期，可按场景改密钥、签发者、受众、有效期与声明。
    /// </remarks>
    public static string Create(string? subject = "alice", string secret = Secret, string issuer = Issuer, string audience = Audience, TimeSpan? lifetime = null, params (string Type, string Value)[] claims)
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
            Issuer = issuer,
            Audience = audience,
            Subject = identity,
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        });
    }
}
