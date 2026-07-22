// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Session;

/// <summary>
/// 会话状态中间件：在鉴权前拦截「已失效」与「已锁定」的会话
/// </summary>
/// <remarks>
/// 管道位置必须是 <c>UseAuthentication</c> → <c>TenantResolve</c> → <b>本中间件</b> → <c>UseAuthorization</c>：
/// <list type="bullet">
///   <item>在认证之后：要读 <c>session_id</c> claim；</item>
///   <item>在租户解析之后：应用侧的会话表通常是多租户实体，租户上下文未解析会被全局过滤器挡掉；</item>
///   <item>在授权之前：锁定(423)/失效(401) 要先于任何权限评估短路，避免与 403 语义混淆。</item>
/// </list>
/// <para>
/// 判定委托给 <see cref="ISessionStateGate"/>（应用侧实现）。框架默认 <see cref="NullSessionStateGate"/> 一律放行。
/// </para>
/// <para>
/// <b>放行条件</b>：端点标记 <see cref="IAllowAnonymous"/>、未认证、无 <c>session_id</c> claim（如开放接口 AccessKey 客户端）、
/// SignalR Hub 路径。锁定另有路径白名单（解锁/登出/刷新）。
/// </para>
/// </remarks>
public sealed class XiHanSessionStateMiddleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISessionStateGate _gate;
    private readonly XiHanSessionStateOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSessionStateMiddleware(ISessionStateGate gate, IOptions<XiHanSessionStateOptions> options)
    {
        _gate = gate;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_options.IsEnabled)
        {
            await next(context);
            return;
        }

        var sessionId = ResolveSessionId(context);
        if (sessionId is null || ShouldSkip(context))
        {
            await next(context);
            return;
        }

        var decision = await _gate.EvaluateAsync(sessionId, context.RequestAborted);

        switch (decision.Status)
        {
            case SessionGateStatus.Invalid:
                // 会话已失效（登出/被踢/过期）：无论什么端点一律 401，客户端回登录页。
                // 这条不设白名单——被踢的会话不该还能刷新令牌或切租户"自我复活"。
                await WriteAsync(context, ApiResponseCodes.Unauthorized, "会话已失效，请重新登录", null);
                return;

            case SessionGateStatus.Locked when !IsLockAllowed(context):
                // 锁定：用户身份仍有效，故回 423 而非 401——客户端应引导解锁，不得跳登录页。
                // 框架不关心锁定的原因（锁屏只是应用侧的一种），只负责拦截。
                // 载荷只带展示名/头像：解锁页要显示"锁的是谁"，而用户信息接口本身是被挡住的。
                await WriteAsync(context, ApiResponseCodes.Locked, "会话已锁定，请先解锁", new
                {
                    // reason 由应用侧定义，框架只透传：客户端据此决定引导哪种解锁方式
                    reason = decision.Reason,
                    displayName = decision.DisplayName,
                    avatarUrl = decision.AvatarUrl
                });
                return;

            default:
                await next(context);
                return;
        }
    }

    /// <summary>
    /// 取 session_id claim；无则视为非会话型调用（开放接口 AccessKey 等），不归本中间件管
    /// </summary>
    private static string? ResolveSessionId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sessionId = context.User.FindFirstValue(XiHanClaimTypes.SessionId);
        return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
    }

    /// <summary>
    /// 匿名端点与 SignalR Hub 整体跳过
    /// </summary>
    private bool ShouldSkip(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_options.SignalRHubPathPrefix)
            && context.Request.Path.StartsWithSegments(_options.SignalRHubPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 锁定白名单（仅对 423 生效）
    /// </summary>
    private bool IsLockAllowed(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return _options.LockAllowedPaths.Exists(
            allowed => path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteAsync(HttpContext context, ApiResponseCodes code, string message, object? data)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString() ?? context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = (int)code;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new ApiResponse
        {
            Code = code,
            Message = message,
            Data = data,
            TraceId = traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), context.RequestAborted);
    }
}
