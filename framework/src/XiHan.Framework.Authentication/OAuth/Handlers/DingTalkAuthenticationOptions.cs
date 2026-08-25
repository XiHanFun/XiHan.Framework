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
    /// 企业 CorpId，随授权请求一并带出
    /// </summary>
    /// <remarks>
    /// 用户选定的组织由钉钉在令牌响应里返回，且只在权限范围含 <c>corpid</c> 时下发。
    /// </remarks>
    public string? CorpId { get; set; }
}
