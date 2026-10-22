// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Auth;

/// <summary>
/// 授权结果处理器：带失败原因的 403 写成统一响应信封
/// </summary>
/// <remarks>
/// 授权处理器调用 <c>context.Fail(new AuthorizationFailureReason(...))</c> 时，403 的消息取第一条非空原因
/// （如「权限不足」「当前条件下不允许此操作」），并带上 traceId；没有失败原因的拒绝与 401 照旧交给 ASP.NET Core 的缺省处理。
/// </remarks>
public sealed class XiHanAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    /// <inheritdoc />
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var message = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailureReasons
                .Select(static reason => reason.Message)
                .FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text))
            : null;
        if (message is null || context.Response.HasStarted)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString() ?? context.TraceIdentifier;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new ApiResponse
        {
            Code = ApiResponseCodes.Forbidden,
            Message = message,
            TraceId = traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}
