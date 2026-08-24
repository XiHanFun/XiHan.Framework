// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 飞书登录选项
/// </summary>
/// <remarks>
/// 默认走 passport 端点（供网页二维码 SDK 内嵌）。改走开放平台端点时三个地址要成套替换，
/// 两套端点的授权码不可交叉换取。
/// </remarks>
public class FeishuAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public FeishuAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.Feishu;
        CallbackPath = new PathString("/signin-feishu");

        AuthorizationEndpoint = OAuthProviderEndpoints.Feishu.QrCodeAuthorization;
        TokenEndpoint = OAuthProviderEndpoints.Feishu.QrCodeToken;
        UserInformationEndpoint = OAuthProviderEndpoints.Feishu.QrCodeUserInformation;

        ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Feishu.OpenId, "open_id");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Feishu.UnionId, "union_id");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Feishu.UserId, "user_id");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Feishu.Mobile, "mobile");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Feishu.Avatar, "avatar_url");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "avatar_url");
    }

    /// <summary>
    /// 令牌接口是否收表单体
    /// </summary>
    /// <remarks>passport 端点收表单，开放平台端点收 JSON。</remarks>
    public bool UseFormTokenRequest { get; set; } = true;
}
