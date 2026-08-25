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
/// GitHub 登录处理器
/// </summary>
/// <remarks>
/// 只在用户信息接口没给出邮箱时补取一次主邮箱，其余沿用基类。
/// </remarks>
public class GitHubAuthenticationHandler : XiHanOAuthHandler<GitHubAuthenticationOptions>
{
    private const string EmailScope = "user:email";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">选项监视器</param>
    /// <param name="logger">日志工厂</param>
    /// <param name="encoder">URL 编码器</param>
    public GitHubAuthenticationHandler(
        IOptionsMonitor<GitHubAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 邮箱声明缺失且申请过邮箱权限时，从邮箱列表接口补取主邮箱
    /// </summary>
    /// <param name="identity">声明标识</param>
    /// <param name="tokens">令牌响应</param>
    /// <param name="payload">用户信息 JSON</param>
    protected override async Task AfterClaimActionsAsync(ClaimsIdentity identity, OAuthTokenResponse tokens, JsonElement payload)
    {
        if (string.IsNullOrEmpty(Options.UserEmailsEndpoint)
            || identity.HasClaim(claim => claim.Type == ClaimTypes.Email)
            || !Options.Scope.Contains(EmailScope))
        {
            return;
        }

        JsonDocument emails;
        try
        {
            emails = await GetBearerJsonAsync(Options.UserEmailsEndpoint, tokens.AccessToken, "拉取邮箱列表");
        }
        catch (AuthenticationFailureException exception)
        {
            // 邮箱只是资料补充，应用未获授权时保持邮箱为空，不因此让整个登录失败
            Logger.LogWarning(exception, "{Scheme} 拉取邮箱列表失败，邮箱声明保持为空。", Scheme.Name);
            return;
        }

        using (emails)
        {
            if (emails.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var primary = emails.RootElement.EnumerateArray()
                .FirstOrDefault(address => address.TryGetProperty("primary", out var flag) && flag.ValueKind == JsonValueKind.True);

            AddEmailClaim(identity, ReadString(primary, "email"));
        }
    }

    private void AddEmailClaim(ClaimsIdentity identity, string? email)
    {
        if (!string.IsNullOrEmpty(email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email, ClaimValueTypes.String, Options.ClaimsIssuer));
        }
    }
}
