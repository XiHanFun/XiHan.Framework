#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentPrincipalAccessorExtensions
// Guid:638b6fd9-5b5d-4667-94d0-1ccb5757b9ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 07:24:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
