// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.OpenIdConnect;

/// <summary>
/// 用签发者 JWKS 里的密钥校验 <c>Bearer</c> 令牌并映射为主体；签名密钥未知时按需刷新一次元数据再试。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenIdConnectAuthenticationHandler : IAuthenticationHandler
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
    /// 选项。
    /// </summary>
    private readonly OpenIdConnectOptions _options;

    /// <summary>
    /// 签发者元数据。
    /// </summary>
    private readonly OpenIdConnectMetadataCache _metadata;

    /// <summary>
    /// 创建处理器。
    /// </summary>
    public OpenIdConnectAuthenticationHandler(OpenIdConnectOptions options, OpenIdConnectMetadataCache metadata)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <inheritdoc />
    public string Scheme => SchemeName;

    /// <inheritdoc />
    public async ValueTask<Result<Principal>> AuthenticateAsync(AuthenticationCredential credential, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        // 头部或载荷解析不出的令牌直接拒绝，不为它访问签发者
        JsonWebToken token;
        try
        {
            token = _handler.ReadJsonWebToken(credential.Value);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenException)
        {
            return InvalidToken();
        }

        var metadata = await _metadata.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!metadata.TryGetValue(out var current))
        {
            return metadata.Error!;
        }

        var validation = await _handler.ValidateTokenAsync(token, Parameters(current)).ConfigureAwait(false);
        if (!validation.IsValid && validation.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // 签名密钥未知多半是签发者轮换了密钥：按需刷新（受最短间隔限制），拿到新的一份才重试
            var refreshed = await _metadata.RefreshAsync(cancellationToken).ConfigureAwait(false);
            if (refreshed.TryGetValue(out var latest) && !ReferenceEquals(latest, current))
            {
                validation = await _handler.ValidateTokenAsync(token, Parameters(latest)).ConfigureAwait(false);
            }
        }

        // 所有校验失败统一归为 InvalidCredential，不向调用方泄露具体原因
        if (!validation.IsValid || validation.ClaimsIdentity is null)
        {
            return InvalidToken();
        }

        // MapInboundClaims 已关闭：sub、role 等声明按原始类型名进入主体，与 JWT、ApiKey 处理器的声明形态一致
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

    /// <summary>
    /// 由选项与一份元数据推出校验参数：签名、过期与有效期始终强制，签发者取选项覆盖或发现文档，受众只在配置了才校验。
    /// </summary>
    private TokenValidationParameters Parameters(OpenIdConnectMetadata metadata) => new()
    {
        IssuerSigningKeys = metadata.SigningKeys,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = _options.ClockSkew,
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer ?? metadata.Issuer,
        ValidateAudience = _options.Audience is not null,
        ValidAudience = _options.Audience,
        NameClaimType = _options.NameClaim,
    };

    /// <summary>
    /// 令牌无效的失败结果。
    /// </summary>
    private static Result<Principal> InvalidToken()
        => Result.Failure<Principal>(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken));
}
