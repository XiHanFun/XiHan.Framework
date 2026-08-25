// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 企业微信登录选项
/// </summary>
public class WorkWeixinAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public WorkWeixinAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.WorkWeixin;
        CallbackPath = new PathString("/signin-workweixin");

        AuthorizationEndpoint = OAuthProviderEndpoints.WorkWeixin.QrCodeAuthorization;
        TokenEndpoint = OAuthProviderEndpoints.WorkWeixin.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.WorkWeixin.UserDetail;

        ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        ClaimActions.MapJsonKey(ClaimTypes.Gender, "gender");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.WorkWeixin.OpenId, "openid");
        ClaimActions.MapJsonKey(OAuthClaimTypes.WorkWeixin.Mobile, "mobile");
        ClaimActions.MapJsonKey(OAuthClaimTypes.WorkWeixin.Avatar, "avatar");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "avatar");
    }

    /// <summary>
    /// 自建应用 AgentId
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// 成员标识接口地址
    /// </summary>
    public string UserIdentificationEndpoint { get; set; } = OAuthProviderEndpoints.WorkWeixin.UserIdentification;

    /// <summary>
    /// 通讯录成员读取接口地址
    /// </summary>
    public string MemberEndpoint { get; set; } = OAuthProviderEndpoints.WorkWeixin.Member;

    /// <summary>
    /// 是否额外读取通讯录成员资料以取回姓名
    /// </summary>
    /// <remarks>
    /// 授权链路本身不返回姓名。开启后按成员标识调用通讯录读取接口补齐，
    /// 读取失败或企业未授权姓名字段时保持姓名为空，不影响登录本身。
    /// </remarks>
    public bool LoadMemberProfile { get; set; }
}
