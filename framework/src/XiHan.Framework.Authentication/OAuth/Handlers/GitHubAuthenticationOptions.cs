// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// GitHub 登录选项
/// </summary>
public class GitHubAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public GitHubAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.GitHub;
        CallbackPath = new PathString("/signin-github");

        AuthorizationEndpoint = OAuthProviderEndpoints.GitHub.Authorization;
        TokenEndpoint = OAuthProviderEndpoints.GitHub.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.GitHub.UserInformation;

        ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.GitHub.Name, "name");
        ClaimActions.MapJsonKey(OAuthClaimTypes.GitHub.Url, "url");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "avatar_url");
    }

    /// <summary>
    /// 邮箱列表接口地址
    /// </summary>
    /// <remarks>
    /// 用户把邮箱设为私密时，用户信息接口的 email 字段为空，需申请 <c>user:email</c> 权限再从此接口取主邮箱。
    /// 置空则不做这一步补取。
    /// </remarks>
    public string UserEmailsEndpoint { get; set; } = OAuthProviderEndpoints.GitHub.UserEmails;
}
