// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Session;

namespace XiHan.Framework.Web.RealTime.Filters;

/// <summary>
/// Hub 会话状态过滤器：连接建立与方法调用时校验会话是否仍然有效
/// </summary>
/// <remarks>
/// <c>XiHanSessionStateMiddleware</c> 对 Hub 路径整体跳过（长连接不能直接回 401/423），
/// 因此已登出、被踢下线、已过期的会话在 Hub 侧不受任何约束。本过滤器补上这条判定：
/// 连接期判失效即拒绝建连，已建连的连接在下一次方法调用时被中止。
/// </remarks>
public sealed class SessionStateHubFilter : IHubFilter
{
    private readonly ILogger<SessionStateHubFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public SessionStateHubFilter(ILogger<SessionStateHubFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 建立连接前校验会话
    /// </summary>
    /// <param name="context">Hub 生命周期上下文</param>
    /// <param name="next">下一个处理器</param>
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        if (!await IsSessionValidAsync(context.Context))
        {
            throw new HubException("会话已失效，请重新登录");
        }

        await next(context);
    }

    /// <summary>
    /// 方法调用前校验会话
    /// </summary>
    /// <param name="invocationContext">调用上下文</param>
    /// <param name="next">下一个处理器</param>
    /// <returns>方法返回值</returns>
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!await IsSessionValidAsync(invocationContext.Context))
        {
            invocationContext.Context.Abort();
            throw new HubException("会话已失效，请重新登录");
        }

        return await next(invocationContext);
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    /// <param name="context">Hub 生命周期上下文</param>
    /// <param name="exception">异常</param>
    /// <param name="next">下一个处理器</param>
    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        return next(context, exception);
    }

    /// <summary>
    /// 取 session_id 声明后走应用侧闸门；无声明（非会话型令牌）放行
    /// </summary>
    private async Task<bool> IsSessionValidAsync(HubCallerContext context)
    {
        var sessionId = context.User?.FindFirstValue(XiHanClaimTypes.SessionId);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return true;
        }

        var gate = context.GetHttpContext()?.RequestServices.GetService<ISessionStateGate>();
        if (gate is null)
        {
            return true;
        }

        try
        {
            var decision = await gate.EvaluateAsync(sessionId, context.ConnectionAborted);
            return decision.Status != SessionGateStatus.Invalid;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 与 HTTP 侧闸门同口径：基础设施异常放行并记 Error，避免一次抖动把所有长连接打断
            _logger.LogError(exception, "Hub 会话闸门评估失败，已放行：{SessionId}", sessionId);
            return true;
        }
    }
}
