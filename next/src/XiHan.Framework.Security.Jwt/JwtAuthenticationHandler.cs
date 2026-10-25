// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

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

    /// <summary>
    /// 令牌处理器；关闭入站声明映射，声明按 JWT 原始类型名进入主体。
    /// </summary>
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    /// <summary>
    /// 由选项推出的校验参数；构造时固定。
    /// </summary>
    private readonly TokenValidationParameters _parameters;

    /// <summary>
    /// JWT 选项；提供主体声明类型。
    /// </summary>
    private readonly JwtOptions _options;

    /// <summary>
    /// 创建处理器。
    /// </summary>
    public JwtAuthenticationHandler(JwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // 签名、exp 与有效期始终强制；签发者与受众只在配置了才校验
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
        // 所有校验失败统一归为 InvalidCredential，不向调用方泄露具体原因
        if (!validation.IsValid || validation.ClaimsIdentity is null)
        {
            return Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken));
        }

        // MapInboundClaims 已关闭：sub、role 等声明按原始类型名进入主体，与 ApiKey 处理器的声明形态一致
        var claims = validation.ClaimsIdentity.Claims.Select(static c => new KeyValuePair<string, string>(c.Type, c.Value)).ToList();
        var subject = validation.ClaimsIdentity.FindFirst(_options.NameClaim)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.TokenSubjectMissing, MessageArguments.Of(("claim", _options.NameClaim))));
        }

        return Result.Success(new Principal(subject, SchemeName)
        {
            DisplayName = validation.ClaimsIdentity.FindFirst("name")?.Value,
            Claims = claims,
        });
    }
}
