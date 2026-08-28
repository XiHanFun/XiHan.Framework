// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Contexts;

/// <summary>
/// 基于 HttpContext 的链路追踪标识提供者
/// </summary>
public class HttpTraceIdProvider : ITraceIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public HttpTraceIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 获取当前请求的链路追踪标识，优先取当前 Activity 的 TraceId，其次取 HttpContext 中的标识
    /// </summary>
    /// <returns>TraceId，不在请求上下文中则返回 null</returns>
    public string? GetCurrentTraceId()
    {
        // 优先 W3C：OTel/ASP.NET Core 已为请求建 Activity 时取其 TraceId（32-hex），跨服务同源可与 trace 后端 join
        var current = Activity.Current;
        if (current is not null && current.TraceId != default)
        {
            return current.TraceId.ToHexString();
        }

        // 回退旧口径（OTel 未激活或非 HTTP 上下文）：Items[__XiHanTraceId] ?? Kestrel TraceIdentifier
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
               ?? httpContext.TraceIdentifier;
    }
}
