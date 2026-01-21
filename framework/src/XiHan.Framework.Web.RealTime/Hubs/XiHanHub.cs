#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHub
// Guid:3e6f7a8b-9c1d-4e2f-9a1b-3c4d5e6f7a8b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using XiHan.Framework.Web.RealTime.Services;

namespace XiHan.Framework.Web.RealTime.Hubs;

/// <summary>
/// 曦寒 Hub 基类
/// </summary>
public abstract class XiHanHub : Hub, IXiHanHub
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionManager"></param>
    protected XiHanHub(IConnectionManager connectionManager)
    {
        ConnectionManager = connectionManager;
    }

    /// <summary>
    /// 获取当前连接 ID
    /// </summary>
    public string? ConnectionId => Context.ConnectionId;

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    public string? UserId => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// 获取当前用户名
    /// </summary>
    public string? UserName => Context.User?.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>
    /// 连接管理器
    /// </summary>
    protected IConnectionManager ConnectionManager { get; }

    /// <summary>
    /// 客户端连接时触发
    /// </summary>
    /// <returns></returns>
    public override async Task OnConnectedAsync()
    {
        if (!string.IsNullOrEmpty(ConnectionId) && !string.IsNullOrEmpty(UserId))
        {
            await ConnectionManager.AddConnectionAsync(UserId, ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开连接时触发
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (!string.IsNullOrEmpty(ConnectionId) && !string.IsNullOrEmpty(UserId))
        {
            await ConnectionManager.RemoveConnectionAsync(UserId, ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
