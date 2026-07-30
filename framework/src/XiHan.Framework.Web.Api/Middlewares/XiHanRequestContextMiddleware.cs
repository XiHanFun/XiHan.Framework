// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Localization.Middlewares;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Web.Core.Clients;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 请求上下文中间件
/// </summary>
public class XiHanRequestContextMiddleware(RequestDelegate next)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <param name="requestContextAccessor"></param>
    /// <param name="clientInfoProvider"></param>
    /// <returns></returns>
    public async Task InvokeAsync(
        HttpContext context,
        IRequestContextAccessor requestContextAccessor,
        IClientInfoProvider clientInfoProvider)
    {
        var currentUser = context.RequestServices.GetService<ICurrentUser>();
        var currentTenant = context.RequestServices.GetService<ICurrentTenant>();
        var clientInfo = clientInfoProvider.GetCurrent();
        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? context.TraceIdentifier;
        var culture = context.Items[XiHanRequestCultureMiddleware.CultureItemKey]?.ToString()
            ?? CultureInfo.CurrentUICulture.Name;

        requestContextAccessor.Current = new RequestContext
        {
            TraceId = traceId,
            Culture = culture,
            RequestId = context.TraceIdentifier,
            UserId = currentUser?.UserId,
            UserName = currentUser?.UserName,
            TenantId = currentTenant?.Id ?? currentUser?.TenantId,
            RemoteIp = clientInfo.IpAddress ?? context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = clientInfo.UserAgent ?? context.Request.Headers.UserAgent.ToString(),
            Path = context.Request.Path.ToString(),
            Method = context.Request.Method,
            StartTime = DateTimeOffset.UtcNow
        };

        await next(context);
    }
}
