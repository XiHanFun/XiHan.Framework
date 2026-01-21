#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HubExceptionFilter
// Guid:fe9f1a2b-3c4d-4e5f-9a2b-fc7d8e9f1a2b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 5:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Web.RealTime.Filters;

/// <summary>
/// Hub 异常过滤器
/// </summary>
public class HubExceptionFilter : IHubFilter
{
    private readonly ILogger<HubExceptionFilter> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public HubExceptionFilter(ILogger<HubExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 方法调用异常处理
    /// </summary>
    /// <param name="invocationContext">调用上下文</param>
    /// <param name="next">下一个处理器</param>
    /// <returns></returns>
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error invoking hub method {MethodName} for user {UserId}",
                invocationContext.HubMethodName,
                invocationContext.Context.User?.Identity?.Name);

            throw new HubException($"调用方法 {invocationContext.HubMethodName} 时发生错误");
        }
    }

    /// <summary>
    /// 连接异常处理
    /// </summary>
    /// <param name="context">Hub 生命周期上下文</param>
    /// <param name="next">下一个处理器</param>
    /// <returns></returns>
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        try
        {
            await next(context);
            _logger.LogInformation(
                "User {UserId} connected to hub with connection {ConnectionId}",
                context.Context.User?.Identity?.Name,
                context.Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error on connection for user {UserId}",
                context.Context.User?.Identity?.Name);
            throw;
        }
    }

    /// <summary>
    /// 断开连接异常处理
    /// </summary>
    /// <param name="context">Hub 生命周期上下文</param>
    /// <param name="exception">异常</param>
    /// <param name="next">下一个处理器</param>
    /// <returns></returns>
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        try
        {
            await next(context, exception);
            _logger.LogInformation(
                "User {UserId} disconnected from hub with connection {ConnectionId}",
                context.Context.User?.Identity?.Name,
                context.Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error on disconnection for user {UserId}",
                context.Context.User?.Identity?.Name);
            throw;
        }
    }
}
