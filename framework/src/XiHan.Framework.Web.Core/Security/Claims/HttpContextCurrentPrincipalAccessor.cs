#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpContextCurrentPrincipalAccessor
// Guid:b5c5807a-bde6-48af-9b57-74d1c846fd30
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/11 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Security.Claims;

namespace XiHan.Framework.Web.Core.Security.Claims;

/// <summary>
/// 基于 HttpContext 的当前主体访问器，使 ICurrentUser 在 Web 请求中获取到 JWT 认证后的 User。
/// </summary>
public class HttpContextCurrentPrincipalAccessor : CurrentPrincipalAccessorBase, IScopedDependency
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    public HttpContextCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    protected override ClaimsPrincipal GetClaimsPrincipal()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
}
