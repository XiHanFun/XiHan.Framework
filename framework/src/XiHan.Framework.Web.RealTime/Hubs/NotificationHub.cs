#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NotificationHub
// Guid:cb6c7d8e-9f1a-4b2c-9d7e-cf4a5b6c7d8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 05:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.SignalR;
using XiHan.Framework.Web.RealTime.Services;

namespace XiHan.Framework.Web.RealTime.Hubs;

/// <summary>
/// 通知 Hub 示例
/// </summary>
public class NotificationHub : XiHanHub
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionManager">连接管理器</param>
    public NotificationHub(IConnectionManager connectionManager)
        : base(connectionManager)
    {
    }

    /// <summary>
    /// 发送消息给指定用户
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="message">消息内容</param>
    /// <returns></returns>
    public async Task SendMessageToUser(string userId, string message)
    {
        var connections = await ConnectionManager.GetConnectionsAsync(userId);
        if (connections.Count > 0)
        {
            await Clients.Clients(connections).SendAsync("ReceiveMessage", UserId, message);
        }
    }

    /// <summary>
    /// 发送消息给所有人
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns></returns>
    public async Task SendMessageToAll(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", UserId, message);
    }

    /// <summary>
    /// 加入组
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(ConnectionId!, groupName);
        await Clients.Group(groupName).SendAsync("UserJoined", UserId, groupName);
    }

    /// <summary>
    /// 离开组
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(ConnectionId!, groupName);
        await Clients.Group(groupName).SendAsync("UserLeft", UserId, groupName);
    }

    /// <summary>
    /// 发送消息给组
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <param name="message">消息内容</param>
    /// <returns></returns>
    public async Task SendMessageToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", UserId, message);
    }

    /// <summary>
    /// 获取在线用户数量
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetOnlineUserCount()
    {
        return await ConnectionManager.GetOnlineUserCountAsync();
    }

    /// <summary>
    /// 检查用户是否在线
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    public async Task<bool> IsUserOnline(string userId)
    {
        return await ConnectionManager.IsUserOnlineAsync(userId);
    }
}
