// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace XiHan.Framework.Security.Claims;

/// <summary>
/// 当前主体访问器
/// </summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// 当前主体
    /// </summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    /// 更改主体
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    IDisposable Change(ClaimsPrincipal principal);
}
