// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace XiHan.Framework.Security.Claims;

/// <summary>
/// 当前主体访问器扩展
/// </summary>
public static class CurrentPrincipalAccessorExtensions
{
    /// <summary>
    /// 更改当前主体的声明
    /// </summary>
    /// <param name="currentPrincipalAccessor"></param>
    /// <param name="claim"></param>
    /// <returns></returns>
    public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, Claim claim)
    {
        return currentPrincipalAccessor.Change([claim]);
    }

    /// <summary>
    /// 更改当前主体的声明集合
    /// </summary>
    /// <param name="currentPrincipalAccessor"></param>
    /// <param name="claims"></param>
    /// <returns></returns>
    public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, IEnumerable<Claim> claims)
    {
        return currentPrincipalAccessor.Change(new ClaimsIdentity(claims));
    }

    /// <summary>
    /// 更改当前主体的声明标识
    /// </summary>
    /// <param name="currentPrincipalAccessor"></param>
    /// <param name="claimsIdentity"></param>
    /// <returns></returns>
    public static IDisposable Change(this ICurrentPrincipalAccessor currentPrincipalAccessor, ClaimsIdentity claimsIdentity)
    {
        return currentPrincipalAccessor.Change(new ClaimsPrincipal(claimsIdentity));
    }
}
