#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentPrincipalAccessor
// Guid:7a406fad-bb43-4e56-b12f-291c618e9576
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 05:05:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
