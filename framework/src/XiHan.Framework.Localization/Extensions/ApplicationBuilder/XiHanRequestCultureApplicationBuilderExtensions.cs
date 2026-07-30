// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Localization.Middlewares;

namespace XiHan.Framework.Localization.Extensions.ApplicationBuilder;

/// <summary>
/// 请求文化中间件应用扩展
/// </summary>
public static class XiHanRequestCultureApplicationBuilderExtensions
{
    /// <summary>
    /// 启用请求文化中间件
    /// 应在路由/MVC 之前注册（建议紧跟 TraceId 等早期中间件），使后续管线在请求文化下执行
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseXiHanRequestCulture(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<XiHanRequestCultureMiddleware>();
    }
}
