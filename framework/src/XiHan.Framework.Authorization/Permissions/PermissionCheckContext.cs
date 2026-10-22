// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using XiHan.Framework.Authorization.AspNetCore;

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 带请求上下文的权限判定输入
/// </summary>
/// <remarks>
/// 由授权处理器构造：权限检查器可据此按请求求值附加条件（如按 IP、时间、资源属性收窄授权）。
/// 菜单、后台任务这类非请求场景仍用 <see cref="IPermissionChecker.IsGrantedAsync"/>。
/// </remarks>
public sealed class PermissionCheckContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="principal">当前用户主体</param>
    /// <param name="resource">授权资源（ASP.NET Core 端点授权时为 <see cref="Microsoft.AspNetCore.Http.HttpContext"/>，MVC 过滤器授权时为带 HttpContext 的过滤器上下文）</param>
    public PermissionCheckContext(string userId, string permissionCode, ClaimsPrincipal? principal = null, object? resource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        UserId = userId;
        PermissionCode = permissionCode;
        Principal = principal;
        Resource = resource;
        HttpContext = AuthorizationResourceHttpContext.TryResolve(resource, out var httpContext) ? httpContext : null;
    }

    /// <summary>
    /// 用户标识
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// 权限编码
    /// </summary>
    public string PermissionCode { get; }

    /// <summary>
    /// 当前用户主体
    /// </summary>
    public ClaimsPrincipal? Principal { get; }

    /// <summary>
    /// 授权资源原样
    /// </summary>
    public object? Resource { get; }

    /// <summary>
    /// 从授权资源解析出的当前请求；非请求场景为空
    /// </summary>
    public HttpContext? HttpContext { get; }
}
