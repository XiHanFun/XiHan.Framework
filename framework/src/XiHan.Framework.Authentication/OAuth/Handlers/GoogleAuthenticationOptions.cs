// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// Google 登录选项
/// </summary>
public class GoogleAuthenticationOptions : XiHanOAuthProviderOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public GoogleAuthenticationOptions()
    {
        ClaimsIssuer = OAuthProviderNames.Google;
        CallbackPath = new PathString("/signin-google");

        AuthorizationEndpoint = OAuthProviderEndpoints.Google.Authorization;
        TokenEndpoint = OAuthProviderEndpoints.Google.Token;
        UserInformationEndpoint = OAuthProviderEndpoints.Google.UserInformation;

        UsePkce = true;

        Scope.Add("openid");
        Scope.Add("profile");
        Scope.Add("email");

        ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
        ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
        ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
        ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        ClaimActions.MapJsonKey(OAuthClaimTypes.Google.Profile, "link");
        ClaimActions.MapJsonKey(OAuthOptions.AvatarClaimType, "picture");
    }
}
