#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpTraceIdProvider
// Guid:5d6e7f80-91a2-b3c4-d5e6-f70819203142
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Http;
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

    /// <inheritdoc />
    public string? GetCurrentTraceId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
               ?? httpContext.TraceIdentifier;
    }
}
