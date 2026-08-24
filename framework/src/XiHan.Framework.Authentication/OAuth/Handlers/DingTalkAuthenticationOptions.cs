// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 钉钉登录选项
/// </summary>
public class DingTalkAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public DingTalkAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.DingTalk;
        CallbackPath = new PathString("/signin-dingtalk");

        AuthorizationEndpoint = OAuthProviderEndpoints.DingTalk.QrCodeAuthorization;
        TokenEndpoint = OAuthProviderEndpoints.DingTalk.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.DingTalk.UserInformation;

        Scope.Add(OAuthProviderEndpoints.DingTalk.Scope);

        ClaimActions.MapJsonKey(ClaimTypes.Name, "nick");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.DingTalk.OpenId, "openId");
        ClaimActions.MapJsonKey(OAuthClaimTypes.DingTalk.UnionId, "unionId");
        ClaimActions.MapJsonKey(OAuthClaimTypes.DingTalk.Mobile, "mobile");
        ClaimActions.MapJsonKey(OAuthClaimTypes.DingTalk.Avatar, "avatarUrl");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "avatarUrl");
    }

    /// <summary>
    /// 企业 CorpId，填写后授权页锁定到该组织
    /// </summary>
    public string? CorpId { get; set; }
}
