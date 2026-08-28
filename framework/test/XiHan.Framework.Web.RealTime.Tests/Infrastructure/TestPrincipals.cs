// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 构造测试用用户主体的工厂
/// </summary>
public static class TestPrincipals
{
    /// <summary>
    /// 测试用认证方案名
    /// </summary>
    public const string AuthenticationType = "TestScheme";

    /// <summary>
    /// 无任何声明的主体
    /// </summary>
    /// <returns></returns>
    public static ClaimsPrincipal Anonymous()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <summary>
    /// 只带 NameIdentifier 声明的主体
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    public static ClaimsPrincipal WithUserId(string userId)
    {
        return FromClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    /// <summary>
    /// 只带 Name 声明的主体
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <returns></returns>
    public static ClaimsPrincipal WithUserName(string userName)
    {
        return FromClaims(new Claim(ClaimTypes.Name, userName));
    }

    /// <summary>
    /// 同时带 NameIdentifier 与 Name 声明的主体
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="userName">用户名</param>
    /// <returns></returns>
    public static ClaimsPrincipal WithUserIdAndName(string userId, string userName)
    {
        return FromClaims(
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName));
    }

    /// <summary>
    /// 用给定声明构造主体
    /// </summary>
    /// <param name="claims">声明集合</param>
    /// <returns></returns>
    public static ClaimsPrincipal FromClaims(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
    }
}
