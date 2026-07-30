// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
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
