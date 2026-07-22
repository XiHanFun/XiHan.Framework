// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Services;

/// <summary>
/// 连接管理器接口
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// 添加连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    Task AddConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// 移除连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    Task RemoveConnectionAsync(string userId, string connectionId);

    /// <summary>
    /// 获取用户的所有连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    Task<IReadOnlyList<string>> GetConnectionsAsync(string userId);

    /// <summary>
    /// 获取所有在线用户
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<string>> GetOnlineUsersAsync();

    /// <summary>
    /// 检查用户是否在线
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    Task<bool> IsUserOnlineAsync(string userId);

    /// <summary>
    /// 获取在线用户数量
    /// </summary>
    /// <returns></returns>
    Task<int> GetOnlineUserCountAsync();
}
