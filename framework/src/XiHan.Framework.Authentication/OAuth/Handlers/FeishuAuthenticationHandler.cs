// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 飞书登录处理器
/// </summary>
/// <remarks>
/// 飞书用响应体里的 code/msg 或 error/error_description 表达失败而不是 HTTP 状态码；
/// 开放平台端点把用户信息包在 data 节点里，passport 端点是平铺的，两者都在此拆开。
/// </remarks>
public class FeishuAuthenticationHandler : XiHanOAuthHandler<FeishuAuthenticationOptions>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public FeishuAuthenticationHandler(
        IOptionsMonitor<FeishuAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 用授权码换取用户令牌
    /// </summary>
    /// <param name="context">授权码交换上下文</param>
    /// <returns>令牌响应</returns>
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["code"] = context.Code,
            ["redirect_uri"] = context.RedirectUri
        };

        // 授权地址由基类构造，启用 PKCE 时会带上 code_challenge，这里必须把 code_verifier 配回去；
        // 用完即从认证属性里移除，避免它随票据序列化进登录 Cookie
        if (context.Properties.Items.TryGetValue(OAuthConstants.CodeVerifierKey, out var codeVerifier)
            && !string.IsNullOrEmpty(codeVerifier))
        {
            form[OAuthConstants.CodeVerifierKey] = codeVerifier;
            context.Properties.Items.Remove(OAuthConstants.CodeVerifierKey);
        }

        var payload = Options.UseFormTokenRequest
            ? await PostFormAsync(Options.TokenEndpoint, form, "换取用户令牌")
            : await PostJsonAsync(Options.TokenEndpoint, form, "换取用户令牌");

        var failure = DescribeFailure(payload.RootElement, "换取用户令牌");
        if (failure is not null)
        {
            payload.Dispose();
            return OAuthTokenResponse.Failed(new AuthenticationFailureException(failure));
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
        using var payload = await GetBearerJsonAsync(Options.UserInformationEndpoint, tokens.AccessToken, "拉取用户信息");

        var failure = DescribeFailure(payload.RootElement, "拉取用户信息");
        if (failure is not null)
        {
            Logger.LogError("{Scheme} {Failure}", Scheme.Name, failure);
            throw new AuthenticationFailureException(failure);
        }

        // 开放平台端点把用户信息包在 data 里，passport 端点直接平铺
        var data = ReadObject(payload.RootElement, "data");
        var user = data.ValueKind == JsonValueKind.Object ? data : payload.RootElement;

        // union_id 在开发者后台范围内唯一且不随应用变化，优先作为登录标识
        var identifier = ReadString(user, "union_id")
            ?? ReadString(user, "open_id")
            ?? throw MissingField("拉取用户信息", "union_id 与 open_id");

        AddNameIdentifier(identity, identifier);

        return await CreateTicketCoreAsync(identity, properties, tokens, user);
    }

    private string? DescribeFailure(JsonElement root, string operation)
    {
        var code = ReadInt32(root, "code");
        if (code is not (null or 0))
        {
            return $"{operation}失败：code={code}，msg={ReadString(root, "msg") ?? "未知错误"}。";
        }

        var error = ReadString(root, "error");
        return error is null
            ? null
            : $"{operation}失败：error={error}，error_description={ReadString(root, "error_description") ?? "未知错误"}。";
    }
}
