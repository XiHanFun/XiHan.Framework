// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 微信登录选项
/// </summary>
/// <remarks>
/// 默认是开放平台网站应用的扫码登录。改成公众号网页授权时要同时换掉授权页地址、权限范围与凭据，
/// 因为扫码与账号授权分属两个应用。
/// </remarks>
public class WeixinAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public WeixinAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.Weixin;
        CallbackPath = new PathString("/signin-weixin");

        AuthorizationEndpoint = OAuthProviderEndpoints.Weixin.QrCodeAuthorization;
        TokenEndpoint = OAuthProviderEndpoints.Weixin.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.Weixin.UserInformation;

        Scope.Add(OAuthProviderEndpoints.Weixin.QrCodeScope);

        ClaimActions.MapJsonKey(ClaimTypes.Name, "nickname");
        ClaimActions.MapJsonKey(ClaimTypes.Gender, "sex");
        ClaimActions.MapJsonKey(ClaimTypes.Country, "country");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Weixin.OpenId, "openid");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Weixin.UnionId, "unionid");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Weixin.Province, "province");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Weixin.City, "city");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Weixin.HeadImageUrl, "headimgurl");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "headimgurl");
    }
}
