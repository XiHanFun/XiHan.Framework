#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IConnectionManager
// Guid:4f7a8b9c-1d2e-4f3a-9b1c-4d5e6f7a8b9c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
