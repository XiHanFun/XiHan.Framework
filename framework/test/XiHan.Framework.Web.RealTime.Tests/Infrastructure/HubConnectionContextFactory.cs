// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 构造 <see cref="HubConnectionContext"/> 的工厂
/// </summary>
/// <remarks>
/// <see cref="HubConnectionContext"/> 没有可继承的空壳构造路径，只能用真实构造函数搭在
/// <see cref="DefaultConnectionContext"/> 上；用户主体经由 <c>IConnectionUserFeature</c> 传入，
/// 这也是真实 SignalR 管道给 <c>IUserIdProvider</c> 提供用户的方式。
/// 选项里的容量类字段显式赋值，避免依赖框架默认值。
/// </remarks>
public static class HubConnectionContextFactory
{
    /// <summary>
    /// 用给定用户主体构造 Hub 连接上下文
    /// </summary>
    /// <param name="user">用户主体，传 null 表示匿名连接</param>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    public static HubConnectionContext Create(ClaimsPrincipal? user, string connectionId = "conn-1")
    {
        var connection = new DefaultConnectionContext(connectionId)
        {
            User = user!
        };

        var options = new HubConnectionContextOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(15),
            ClientTimeoutInterval = TimeSpan.FromSeconds(30),
            StreamBufferCapacity = 10,
            MaximumParallelInvocations = 1
        };

        return new HubConnectionContext(connection, options, NullLoggerFactory.Instance);
    }
}
