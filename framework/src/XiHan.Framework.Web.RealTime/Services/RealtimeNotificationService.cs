#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RealtimeNotificationService
// Guid:9e3f4a5b-6c7d-4e3f-9a4b-9c1d2e3f4a5b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.SignalR;
using XiHan.Framework.Web.RealTime.Hubs;

namespace XiHan.Framework.Web.RealTime.Services;

/// <summary>
/// 实时通知服务
/// </summary>
/// <typeparam name="THub">Hub 类型</typeparam>
public class RealtimeNotificationService<THub> : IRealtimeNotificationService
    where THub : XiHanHub
{
    private readonly IHubContext<THub> _hubContext;
    private readonly IConnectionManager _connectionManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="hubContext">Hub 上下文</param>
    /// <param name="connectionManager">连接管理器</param>
    public RealtimeNotificationService(
        IHubContext<THub> hubContext,
        IConnectionManager connectionManager)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
    }

    /// <summary>
    /// 向指定用户发送通知
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    public async Task SendToUserAsync(string userId, string method, params object[] args)
    {
        var connections = await _connectionManager.GetConnectionsAsync(userId);
        if (connections.Count > 0)
        {
            await _hubContext.Clients.Clients(connections).SendCoreAsync(method, args);
        }
    }

    /// <summary>
    /// 向指定用户列表发送通知
    /// </summary>
    /// <param name="userIds">用户 ID 列表</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    public async Task SendToUsersAsync(IReadOnlyList<string> userIds, string method, params object[] args)
    {
        var allConnections = new List<string>();
        foreach (var userId in userIds)
        {
            var connections = await _connectionManager.GetConnectionsAsync(userId);
            allConnections.AddRange(connections);
        }

        if (allConnections.Count > 0)
        {
            await _hubContext.Clients.Clients(allConnections).SendCoreAsync(method, args);
        }
    }

    /// <summary>
    /// 向所有用户发送通知
    /// </summary>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    public async Task SendToAllAsync(string method, params object[] args)
    {
        await _hubContext.Clients.All.SendCoreAsync(method, args);
    }

    /// <summary>
    /// 向指定组发送通知
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    public async Task SendToGroupAsync(string groupName, string method, params object[] args)
    {
        await _hubContext.Clients.Group(groupName).SendCoreAsync(method, args);
    }

    /// <summary>
    /// 将用户添加到组
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public async Task AddToGroupAsync(string userId, string groupName)
    {
        var connections = await _connectionManager.GetConnectionsAsync(userId);
        foreach (var connectionId in connections)
        {
            await _hubContext.Groups.AddToGroupAsync(connectionId, groupName);
        }
    }

    /// <summary>
    /// 将用户从组中移除
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public async Task RemoveFromGroupAsync(string userId, string groupName)
    {
        var connections = await _connectionManager.GetConnectionsAsync(userId);
        foreach (var connectionId in connections)
        {
            await _hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName);
        }
    }
}
