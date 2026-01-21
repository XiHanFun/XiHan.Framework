#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayApplicationBuilderExtensions
// Guid:7e8f9a0b-1c2d-3e4f-5a6b-7c8d9e0f1a2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Web.Gateway.Middlewares;

namespace XiHan.Framework.Web.Gateway.Extensions;

/// <summary>
/// 网关应用构建器扩展
/// </summary>
public static class GatewayApplicationBuilderExtensions
{
    /// <summary>
    /// 使用网关中间件
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseGateway(this IApplicationBuilder app)
    {
        // 异常处理必须在最外层
        app.UseMiddleware<GatewayExceptionMiddleware>();

        // 请求追踪
        app.UseMiddleware<RequestTracingMiddleware>();

        // 灰度路由决策
        app.UseMiddleware<GrayRoutingMiddleware>();

        return app;
    }

    /// <summary>
    /// 使用灰度路由
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseGrayRouting(this IApplicationBuilder app)
    {
        app.UseMiddleware<GrayRoutingMiddleware>();
        return app;
    }

    /// <summary>
    /// 使用请求追踪
    /// </summary>
    /// <param name="app">应用构建器</param>
    /// <returns>应用构建器</returns>
    public static IApplicationBuilder UseRequestTracing(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestTracingMiddleware>();
        return app;
    }
}
