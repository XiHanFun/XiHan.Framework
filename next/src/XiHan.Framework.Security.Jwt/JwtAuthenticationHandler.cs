// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.Jwt;

/// <summary>
/// <c>Bearer</c> 方案的 JWT 处理器：HS256 对称密钥校验签名、有效期、签发者与受众，声明按原始类型名映射为主体。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class JwtAuthenticationHandler : IAuthenticationHandler
{
    /// <summary>
    /// 方案名 <c>Bearer</c>。
    /// </summary>
    public const string SchemeName = "Bearer";

    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly TokenValidationParameters _parameters;
    private readonly JwtOptions _options;

    /// <summary>
    /// 创建处理器。
    /// </summary>
    public JwtAuthenticationHandler(JwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _parameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = options.ClockSkew,
            ValidateIssuer = options.Issuer is not null,
            ValidIssuer = options.Issuer,
            ValidateAudience = options.Audience is not null,
            ValidAudience = options.Audience,
            NameClaimType = options.NameClaim,
        };
    }

    /// <inheritdoc />
    public string Scheme => SchemeName;

    /// <inheritdoc />
    public async ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        var validation = await _handler.ValidateTokenAsync(credential.Value, _parameters).ConfigureAwait(false);
        if (!validation.IsValid || validation.ClaimsIdentity is null)
        {
            return Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, "令牌无效、过期或签名不符。"));
        }

        var claims = validation.ClaimsIdentity.Claims.Select(static c => new KeyValuePair<string, string>(c.Type, c.Value)).ToList();
        var subject = validation.ClaimsIdentity.FindFirst(_options.NameClaim)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, $"令牌缺少主体声明 '{_options.NameClaim}'。"));
        }

        return Result.Success(new Principal(subject, SchemeName)
        {
            DisplayName = validation.ClaimsIdentity.FindFirst("name")?.Value,
            Claims = claims,
        });
    }
}
