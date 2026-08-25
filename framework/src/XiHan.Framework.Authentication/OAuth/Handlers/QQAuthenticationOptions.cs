// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// QQ 登录选项
/// </summary>
public class QQAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public QQAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.QQ;
        CallbackPath = new PathString("/signin-qq");

        AuthorizationEndpoint = OAuthProviderEndpoints.QQ.Authorization;
        TokenEndpoint = OAuthProviderEndpoints.QQ.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.QQ.UserInformation;

        Scope.Add("get_user_info");

        ClaimActions.MapJsonKey(ClaimTypes.Name, "nickname");
        ClaimActions.MapJsonKey(ClaimTypes.Gender, "gender");
        ClaimActions.MapJsonKey(OAuthClaimTypes.QQ.PictureUrl, "figureurl");
        ClaimActions.MapJsonKey(OAuthClaimTypes.QQ.PictureMediumUrl, "figureurl_1");
        ClaimActions.MapJsonKey(OAuthClaimTypes.QQ.PictureFullUrl, "figureurl_2");
        ClaimActions.MapJsonKey(OAuthClaimTypes.QQ.AvatarUrl, "figureurl_qq_1");
        ClaimActions.MapJsonKey(OAuthClaimTypes.QQ.AvatarFullUrl, "figureurl_qq_2");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "figureurl_qq_2");
    }

    /// <summary>
    /// 用户标识接口地址
    /// </summary>
    public string UserIdentificationEndpoint { get; set; } = OAuthProviderEndpoints.QQ.UserIdentification;

    /// <summary>
    /// 是否同时申请 UnionId
    /// </summary>
    /// <remarks>需要先在 QQ 互联开放平台申请开通，未开通时请求会失败。</remarks>
    public bool ApplyForUnionId { get; set; }
}
