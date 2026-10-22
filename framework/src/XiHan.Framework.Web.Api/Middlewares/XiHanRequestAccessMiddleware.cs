// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Core.Access;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// 请求访问中间件：依次询问应用注册的 <see cref="IRequestAccessGate"/>，第一个拒绝即以统一响应信封短路
/// </summary>
/// <remarks>
/// 管道位置是 <c>UseAuthentication</c> → <c>TenantResolve</c> → <b>本中间件</b> → <c>SessionState</c> → <c>UseAuthorization</c>：
/// 认证与租户解析之后（闸门可按用户、角色、租户判定），授权之前（被拦的请求不进入权限评估）。
/// 没有注册任何闸门时直接放行。
/// </remarks>
public sealed class XiHanRequestAccessMiddleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IRequestAccessGate[] _gates;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="gates">应用注册的闸门</param>
    public XiHanRequestAccessMiddleware(IEnumerable<IRequestAccessGate> gates)
    {
        _gates = [.. gates.OrderBy(static gate => gate.Order)];
    }

    /// <summary>
    /// 依次判定，拒绝即写响应并短路，全部放行才进入下一个中间件
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    /// <param name="next">请求管道中的下一个中间件</param>
    /// <returns>异步任务</returns>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        foreach (var gate in _gates)
        {
            var decision = await gate.EvaluateAsync(context, context.RequestAborted);
            if (!decision.IsAllowed)
            {
                await WriteAsync(context, decision);
                return;
            }
        }

        await next(context);
    }

    private static async Task WriteAsync(HttpContext context, RequestAccessDecision decision)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString() ?? context.TraceIdentifier;
        context.Response.Clear();
        context.Response.StatusCode = decision.StatusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        foreach (var (name, value) in decision.Headers)
        {
            context.Response.Headers[name] = value;
        }

        var payload = new ApiResponse
        {
            // 状态码有对应的业务码时用它，否则按 403 归类
            Code = Enum.IsDefined(typeof(ApiResponseCodes), decision.StatusCode)
                ? (ApiResponseCodes)decision.StatusCode
                : ApiResponseCodes.Forbidden,
            Message = decision.Message ?? string.Empty,
            Data = decision.Data,
            TraceId = traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}
