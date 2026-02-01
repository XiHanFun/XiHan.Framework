#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GrayRoutingMiddleware
// Guid:2f3a4b5c-6d7e-8f9a-0b1c-2d3e4f5a6b7c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Web.Gateway.Constants;

namespace XiHan.Framework.Web.Gateway.Middlewares;

/// <summary>
/// 灰度路由中间件
/// </summary>
/// <remarks>
/// 职责：
/// 1. 构建灰度上下文
/// 2. 执行灰度决策
/// 3. 将决策结果注入到 HttpContext
/// 4. 不负责具体的路由转发（由后续中间件处理）
/// </remarks>
public class GrayRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IGrayRuleEngine _grayRuleEngine;
    private readonly ILogger<GrayRoutingMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public GrayRoutingMiddleware(
        RequestDelegate next,
        IGrayRuleEngine grayRuleEngine,
        ILogger<GrayRoutingMiddleware> logger)
    {
        _next = next;
        _grayRuleEngine = grayRuleEngine;
        _logger = logger;
    }

    /// <summary>
    /// 执行中间件
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ICurrentTenant? currentTenant = null)
    {
        // 构建灰度上下文
        var grayContext = BuildGrayContext(context, currentTenant);

        // 执行灰度决策
        var decision = await _grayRuleEngine.DecideAsync(grayContext, context.RequestAborted);

        // 将决策结果注入到 HttpContext
        context.Items[GatewayConstants.GrayDecisionKey] = decision;

        // 记录日志
        if (decision.IsGray)
        {
            _logger.LogInformation(
                "灰度决策: {Path} -> {TargetVersion}, RuleId: {RuleId}, Reason: {Reason}",
                context.Request.Path,
                decision.TargetVersion,
                decision.MatchedRuleId,
                decision.Reason);
        }

        // 调用下一个中间件
        await _next(context);
    }

    /// <summary>
    /// 构建灰度上下文
    /// </summary>
    private GrayContext BuildGrayContext(HttpContext httpContext, ICurrentTenant? currentTenant)
    {
        var context = new GrayContext
        {
            RequestPath = httpContext.Request.Path.Value,
            RequestMethod = httpContext.Request.Method,
            ClientIpAddress = httpContext.Connection.RemoteIpAddress?.ToString()
        };

        // 提取用户ID（从Claims或Header）
        context.UserId = httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.User?.FindFirst("userId")?.Value
            ?? httpContext.Request.Headers["X-User-Id"].FirstOrDefault();

        // 提取租户ID
        if (currentTenant?.Id != null)
        {
            context.TenantId = currentTenant.Id;
        }

        // 提取所有Header
        foreach (var header in httpContext.Request.Headers)
        {
            context.Headers![header.Key] = header.Value.ToString();
        }

        return context;
    }
}
