#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTraceIdMiddleware
// Guid:d34bc55b-a9c8-4f32-b627-3f04775d1b58
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi TraceId 注入中间件
/// </summary>
public class XiHanTraceIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Request.Headers[XiHanWebApiConstants.TraceIdHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            traceId = context.TraceIdentifier;
        }

        context.Items[XiHanWebApiConstants.TraceIdItemKey] = traceId;
        context.Response.Headers[XiHanWebApiConstants.TraceIdHeaderName] = traceId;

        await next(context);
    }
}
