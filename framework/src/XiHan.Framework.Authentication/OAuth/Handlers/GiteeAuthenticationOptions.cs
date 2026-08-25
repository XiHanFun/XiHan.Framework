// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// Gitee 登录选项
/// </summary>
public class GiteeAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public GiteeAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.Gitee;
        CallbackPath = new PathString("/signin-gitee");

        AuthorizationEndpoint = OAuthProviderEndpoints.Gitee.Authorization;
        TokenEndpoint = OAuthProviderEndpoints.Gitee.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.Gitee.UserInformation;

        Scope.Add("user_info");
        Scope.Add("emails");

        ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Gitee.Name, "name");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Gitee.Url, "url");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "avatar_url");
    }

    /// <summary>
    /// 邮箱列表接口地址
    /// </summary>
    /// <remarks>用户信息接口没给出邮箱时，申请了 <c>emails</c> 权限就从此接口补取。置空则不做这一步补取。</remarks>
    public string UserEmailsEndpoint { get; set; } = OAuthProviderEndpoints.Gitee.UserEmails;
}
