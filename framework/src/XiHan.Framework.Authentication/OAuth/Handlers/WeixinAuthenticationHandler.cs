// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 微信登录处理器
/// </summary>
/// <remarks>
/// 微信的令牌接口用 appid/secret 而不是 client_id/client_secret，用 errcode 而不是 HTTP 状态码表达失败，
/// 且授权地址必须以 #wechat_redirect 结尾；公众号网页授权还限制 state 长度，需要把状态挪进回调地址。
/// </remarks>
public class WeixinAuthenticationHandler : XiHanOAuthHandler<WeixinAuthenticationOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public WeixinAuthenticationHandler(
        IOptionsMonitor<WeixinAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    private bool UsesShortState => !string.Equals(
        Options.AuthorizationEndpoint,
        OAuthProviderEndpoints.Weixin.QrCodeAuthorization,
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 回调时把挪进回调地址的状态串还原到 state 参数上
    /// </summary>
    /// <returns>处理结果</returns>
    protected override Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        WeixinShortState.Restore(Request);
        return base.HandleRemoteAuthenticateAsync();
    }

    /// <summary>
    /// 构造微信授权地址
    /// </summary>
    /// <param name="properties">认证属性</param>
    /// <param name="redirectUri">回调地址</param>
    /// <returns>授权地址</returns>
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
    {
        var scopes = properties.GetParameter<ICollection<string>>(OAuthChallengeProperties.ScopeKey);
        var state = Options.StateDataFormat.Protect(properties);

        if (UsesShortState)
        {
            (redirectUri, state) = WeixinShortState.Apply(redirectUri, state);
        }

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["appid"] = Options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scopes is null ? FormatScope() : FormatScope(scopes),
            ["state"] = state
        };

        foreach (var parameter in Options.AdditionalAuthorizationParameters)
        {
            parameters[parameter.Key] = parameter.Value;
        }

        return QueryHelpers.AddQueryString(Options.AuthorizationEndpoint, parameters)
            + OAuthProviderEndpoints.WeixinRedirectFragment;
    }

    /// <summary>
    /// 用授权码换取访问令牌
    /// </summary>
    /// <param name="context">授权码交换上下文</param>
    /// <returns>令牌响应</returns>
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var tokenUrl = QueryHelpers.AddQueryString(Options.TokenEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["appid"] = Options.ClientId,
            ["secret"] = Options.ClientSecret,
            ["code"] = context.Code,
            ["grant_type"] = "authorization_code"
        });

        var payload = await GetJsonAsync(tokenUrl, "换取访问令牌");

        var errCode = ReadInt32(payload.RootElement, "errcode");
        if (errCode is not (null or 0))
        {
            var errMessage = ReadString(payload.RootElement, "errmsg") ?? "未知错误";
            payload.Dispose();
            Logger.LogError("{Scheme} 换取访问令牌失败，errcode={ErrCode}，errmsg={ErrMessage}。", Scheme.Name, errCode, errMessage);
            return OAuthTokenResponse.Failed(new AuthenticationFailureException($"换取访问令牌失败：errcode={errCode}，errmsg={errMessage}。"));
        }

        return OAuthTokenResponse.Success(payload);
    }

    /// <summary>
    /// 拉取用户信息并生成认证票据
    /// </summary>
    /// <param name="identity">声明标识</param>
    /// <param name="properties">认证属性</param>
    /// <param name="tokens">令牌响应</param>
    /// <returns>认证票据</returns>
    protected override async Task<AuthenticationTicket> CreateTicketAsync(
        ClaimsIdentity identity,
        AuthenticationProperties properties,
        OAuthTokenResponse tokens)
    {
        var tokenPayload = tokens.Response?.RootElement ?? default;
        var openId = ReadString(tokenPayload, "openid") ?? throw MissingField("换取访问令牌", "openid");
        var unionId = ReadString(tokenPayload, "unionid");
        var grantedScope = ReadString(tokenPayload, "scope") ?? string.Empty;

        // snsapi_base 只授予 openid，拉取用户资料会被拒绝
        if (!GrantsUserInfo(grantedScope))
        {
            AddNameIdentifier(identity, unionId ?? openId);
            return await CreateTicketCoreAsync(identity, properties, tokens, tokenPayload);
        }

        var userInfoUrl = QueryHelpers.AddQueryString(Options.UserInformationEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["access_token"] = tokens.AccessToken,
            ["openid"] = openId,
            ["lang"] = "zh_CN"
        });

        using var user = await GetJsonAsync(userInfoUrl, "拉取用户信息");
        EnsureErrCodeSuccess(user.RootElement, "拉取用户信息");

        // unionid 在开放平台下跨应用唯一，优先作为登录标识
        var identifier = ReadString(user.RootElement, "unionid") ?? unionId ?? ReadString(user.RootElement, "openid") ?? openId;
        AddNameIdentifier(identity, identifier);

        return await CreateTicketCoreAsync(identity, properties, tokens, user.RootElement);
    }

    /// <summary>
    /// 格式化权限范围
    /// </summary>
    /// <param name="scopes">权限范围</param>
    /// <returns>以逗号分隔的权限范围</returns>
    protected override string FormatScope(IEnumerable<string> scopes)
    {
        return string.Join(',', scopes);
    }

    private static bool GrantsUserInfo(string grantedScope)
    {
        return grantedScope.Contains(OAuthProviderEndpoints.Weixin.AccountScope, StringComparison.OrdinalIgnoreCase)
            || grantedScope.Contains(OAuthProviderEndpoints.Weixin.QrCodeScope, StringComparison.OrdinalIgnoreCase);
    }
}
