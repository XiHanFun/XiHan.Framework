// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// QQ 登录处理器
/// </summary>
/// <remarks>
/// QQ 的令牌接口默认返回表单文本、用户标识接口默认返回 JSONP，两处都靠 <c>fmt=json</c> 换成纯 JSON；
/// 用户信息要先换 openid 再取资料，因此比通用形态多一跳。
/// </remarks>
public class QQAuthenticationHandler : XiHanOAuthHandler<QQAuthenticationOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public QQAuthenticationHandler(
        IOptionsMonitor<QQAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 用授权码换取访问令牌
    /// </summary>
    /// <param name="context">授权码交换上下文</param>
    /// <returns>令牌响应</returns>
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["redirect_uri"] = context.RedirectUri,
            ["code"] = context.Code,
            ["grant_type"] = "authorization_code",
            ["fmt"] = "json"
        };

        if (context.Properties.Items.TryGetValue(OAuthConstants.CodeVerifierKey, out var codeVerifier))
        {
            parameters[OAuthConstants.CodeVerifierKey] = codeVerifier;
            context.Properties.Items.Remove(OAuthConstants.CodeVerifierKey);
        }

        var payload = await GetJsonAsync(QueryHelpers.AddQueryString(Options.TokenEndpoint, parameters), "换取访问令牌");

        var error = ReadString(payload.RootElement, "error");
        if (error is not null)
        {
            var description = ReadString(payload.RootElement, "error_description") ?? "未知错误";
            payload.Dispose();
            Logger.LogError("{Scheme} 换取访问令牌失败，error={Error}，error_description={Description}。", Scheme.Name, error, description);
            return OAuthTokenResponse.Failed(new AuthenticationFailureException($"换取访问令牌失败：error={error}，error_description={description}。"));
        }

        return OAuthTokenResponse.Success(payload);
    }

    /// <summary>
    /// 先换取 openid 再拉取用户资料
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
        var (openId, unionId) = await GetUserIdentifierAsync(tokens);

        AddNameIdentifier(identity, openId);
        if (!string.IsNullOrEmpty(unionId))
        {
            identity.AddClaim(new Claim(OAuthClaimTypes.QQ.UnionId, unionId, ClaimValueTypes.String, Options.ClaimsIssuer));
        }

        var userInfoUrl = QueryHelpers.AddQueryString(Options.UserInformationEndpoint, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = Options.ClientId,
            ["access_token"] = tokens.AccessToken,
            ["openid"] = openId
        });

        using var payload = await GetJsonAsync(userInfoUrl, "拉取用户信息");

        var returnCode = ReadInt32(payload.RootElement, "ret");
        if (returnCode is not (null or 0))
        {
            var message = ReadString(payload.RootElement, "msg") ?? "未知错误";
            Logger.LogError("{Scheme} 拉取用户信息失败，ret={ReturnCode}，msg={Message}。", Scheme.Name, returnCode, message);
            throw new AuthenticationFailureException($"拉取用户信息失败：ret={returnCode}，msg={message}。");
        }

        return await CreateTicketCoreAsync(identity, properties, tokens, payload.RootElement);
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

    private async Task<(string OpenId, string? UnionId)> GetUserIdentifierAsync(OAuthTokenResponse tokens)
    {
        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["access_token"] = tokens.AccessToken,
            ["fmt"] = "json"
        };

        if (Options.ApplyForUnionId)
        {
            parameters["unionid"] = "1";
        }

        using var payload = await GetJsonAsync(
            QueryHelpers.AddQueryString(Options.UserIdentificationEndpoint, parameters),
            "换取用户标识");

        var errorCode = ReadInt32(payload.RootElement, "error");
        if (errorCode is not (null or 0))
        {
            var description = ReadString(payload.RootElement, "error_description") ?? "未知错误";
            Logger.LogError("{Scheme} 换取用户标识失败，error={Error}，error_description={Description}。", Scheme.Name, errorCode, description);
            throw new AuthenticationFailureException($"换取用户标识失败：error={errorCode}，error_description={description}。");
        }

        var openId = ReadString(payload.RootElement, "openid");
        return string.IsNullOrEmpty(openId)
            ? throw MissingField("换取用户标识", "openid")
            : (openId, ReadString(payload.RootElement, "unionid"));
    }
}
