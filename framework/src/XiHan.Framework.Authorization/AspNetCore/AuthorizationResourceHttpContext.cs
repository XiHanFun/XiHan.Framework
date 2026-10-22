// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using System.Reflection;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 从授权资源里取当前请求
/// </summary>
internal static class AuthorizationResourceHttpContext
{
    /// <summary>
    /// 资源本身是 <see cref="HttpContext"/>（端点授权），或带公开 HttpContext 属性（MVC 过滤器上下文）时取出
    /// </summary>
    public static bool TryResolve(object? resource, out HttpContext httpContext)
    {
        httpContext = default!;
        switch (resource)
        {
            case HttpContext directHttpContext:
                httpContext = directHttpContext;
                return true;
            case null:
                return false;
        }

        var property = resource.GetType().GetProperty("HttpContext", BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(resource) is HttpContext resolvedHttpContext)
        {
            httpContext = resolvedHttpContext;
            return true;
        }

        return false;
    }
}
