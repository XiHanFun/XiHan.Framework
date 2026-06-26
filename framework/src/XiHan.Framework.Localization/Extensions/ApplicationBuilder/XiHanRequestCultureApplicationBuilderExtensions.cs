#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRequestCultureApplicationBuilderExtensions
// Guid:b1d7e904-3c52-4a86-9f17-6e2a8d4c5b31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/23 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
