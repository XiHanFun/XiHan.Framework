// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 钉钉登录处理器
/// </summary>
/// <remarks>
/// 钉钉的令牌接口收 JSON 体、返回小驼峰字段，用户信息接口用私有请求头携带令牌，
/// 三处都与 OAuth2 通用约定不同，因此逐个改写。
/// </remarks>
public class DingTalkAuthenticationHandler : XiHanOAuthHandler<DingTalkAuthenticationOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public DingTalkAuthenticationHandler(
        IOptionsMonitor<DingTalkAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 构造钉钉授权地址
    /// </summary>
    /// <param name="properties">认证属性</param>
    /// <param name="redirectUri">回调地址</param>
    /// <returns>授权地址</returns>
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri)
    {
        var scopes = properties.GetParameter<ICollection<string>>(OAuthChallengeProperties.ScopeKey);

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = Options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scopes is null ? FormatScope() : FormatScope(scopes),
            ["prompt"] = "consent",
            ["state"] = Options.StateDataFormat.Protect(properties)
        };

        if (!string.IsNullOrWhiteSpace(Options.CorpId))
        {
            parameters["corpId"] = Options.CorpId;
        }

        foreach (var parameter in Options.AdditionalAuthorizationParameters)
        {
            parameters[parameter.Key] = parameter.Value;
        }

        return QueryHelpers.AddQueryString(Options.AuthorizationEndpoint, parameters);
    }

    /// <summary>
    /// 用授权码换取用户令牌
    /// </summary>
    /// <param name="context">授权码交换上下文</param>
    /// <returns>令牌响应</returns>
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        JsonDocument payload;
        try
        {
            payload = await PostJsonAsync(
                Options.TokenEndpoint,
                new
                {
                    clientId = Options.ClientId,
                    clientSecret = Options.ClientSecret,
                    code = context.Code,
                    grantType = "authorization_code"
                },
                "换取用户令牌");
        }
        catch (AuthenticationFailureException exception)
        {
            return OAuthTokenResponse.Failed(exception);
        }

        using (payload)
        {
            var accessToken = ReadString(payload.RootElement, "accessToken");
            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogError("{Scheme} 换取用户令牌失败，响应缺少 accessToken。", Scheme.Name);
                return OAuthTokenResponse.Failed(new AuthenticationFailureException("换取用户令牌失败：响应缺少 accessToken。"));
            }

            // 钉钉返回 accessToken/refreshToken/expireIn，在此改写为 OAuthTokenResponse 读取的标准字段名
            var normalized = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["token_type"] = "Bearer",
                ["access_token"] = accessToken
            };

            var refreshToken = ReadString(payload.RootElement, "refreshToken");
            if (refreshToken is not null)
            {
                normalized["refresh_token"] = refreshToken;
            }

            var expireIn = ReadInt32(payload.RootElement, "expireIn");
            if (expireIn is not null)
            {
                normalized["expires_in"] = expireIn.Value;
            }

            return OAuthTokenResponse.Success(JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(normalized)));
        }
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
        var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint);
        request.Headers.TryAddWithoutValidation(OAuthProviderEndpoints.DingTalk.AccessTokenHeaderName, tokens.AccessToken);

        using var payload = await SendJsonAsync(request, "拉取用户信息");

        // 钉钉成功响应不带 code 字段，出错时以 code/message 描述
        var errorCode = ReadString(payload.RootElement, "code");
        if (errorCode is not null)
        {
            var message = ReadString(payload.RootElement, "message") ?? "未知错误";
            Logger.LogError("{Scheme} 拉取用户信息失败，code={Code}，message={Message}。", Scheme.Name, errorCode, message);
            throw new AuthenticationFailureException($"拉取用户信息失败：code={errorCode}，message={message}。");
        }

        // unionId 在企业范围内唯一且不随应用变化，优先作为登录标识
        var identifier = ReadString(payload.RootElement, "unionId")
            ?? ReadString(payload.RootElement, "openId")
            ?? throw MissingField("拉取用户信息", "unionId 与 openId");

        AddNameIdentifier(identity, identifier);

        return await CreateTicketCoreAsync(identity, properties, tokens, payload.RootElement);
    }
}
