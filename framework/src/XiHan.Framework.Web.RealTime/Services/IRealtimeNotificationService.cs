// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Hubs;

namespace XiHan.Framework.Web.RealTime.Services;

/// <summary>
/// 实时通知服务接口
/// </summary>
public interface IRealtimeNotificationService<THub>
    where THub : XiHanHub
{
    /// <summary>
    /// 向指定用户发送通知
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    Task SendToUserAsync(string userId, string method, params object[] args);

    /// <summary>
    /// 向指定用户列表发送通知
    /// </summary>
    /// <param name="userIds">用户 ID 列表</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    Task SendToUsersAsync(IReadOnlyList<string> userIds, string method, params object[] args);

    /// <summary>
    /// 向所有用户发送通知
    /// </summary>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    Task SendToAllAsync(string method, params object[] args);

    /// <summary>
    /// 向指定组发送通知
    /// </summary>
    /// <param name="groupName">组名</param>
    /// <param name="method">方法名</param>
    /// <param name="args">参数</param>
    /// <returns></returns>
    Task SendToGroupAsync(string groupName, string method, params object[] args);

    /// <summary>
    /// 将用户添加到组
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    Task AddToGroupAsync(string userId, string groupName);

    /// <summary>
    /// 将用户从组中移除
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="groupName">组名</param>
    /// <returns></returns>
    Task RemoveFromGroupAsync(string userId, string groupName);
}
