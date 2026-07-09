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

using System.Diagnostics;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi TraceId 注入中间件
/// </summary>
/// <remarks>
/// OTel 激活后请求 Activity 由 ASP.NET Core 建（早于本中间件），此处仅把 W3C TraceId 镜像进 Items/响应头，
/// 供既有读取方（HttpTraceIdProvider/日志/审计）兼容；未激活时回退旧口径（X-Trace-Id 头 / Kestrel TraceIdentifier）。
/// </remarks>
public class XiHanTraceIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // 优先 W3C Activity TraceId（32-hex）
        var current = Activity.Current;
        var traceId = current is not null && current.TraceId != default
            ? current.TraceId.ToHexString()
            : null;

        // 回退：入站 X-Trace-Id 头（向后兼容）→ Kestrel TraceIdentifier
        if (string.IsNullOrWhiteSpace(traceId))
        {
            traceId = context.Request.Headers[XiHanWebApiConstants.TraceIdHeaderName].FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(traceId))
        {
            traceId = context.TraceIdentifier;
        }

        context.Items[XiHanWebApiConstants.TraceIdItemKey] = traceId;
        context.Response.Headers[XiHanWebApiConstants.TraceIdHeaderName] = traceId;

        await next(context);
    }
}
