// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
