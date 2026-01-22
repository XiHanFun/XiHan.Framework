#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConnectionManager
// Guid:5a8b9c1d-2e3f-4a1b-9c2d-5e6f7a8b9c1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Web.RealTime.Services;

/// <summary>
/// 连接管理器
/// </summary>
public class ConnectionManager : IConnectionManager
{
    /// <summary>
    /// 用户连接映射表（用户ID -> 连接ID集合）
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

    /// <summary>
    /// 连接锁对象
    /// </summary>
    private readonly Lock _connectionLock = new();

    /// <summary>
    /// 添加连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    public Task AddConnectionAsync(string userId, string connectionId)
    {
        lock (_connectionLock)
        {
            var connections = _userConnections.GetOrAdd(userId, _ => []);
            connections.Add(connectionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 移除连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    /// <returns></returns>
    public Task RemoveConnectionAsync(string userId, string connectionId)
    {
        lock (_connectionLock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);

                // 如果用户没有活动连接，从字典中移除
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取用户的所有连接
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    public Task<IReadOnlyList<string>> GetConnectionsAsync(string userId)
    {
        lock (_connectionLock)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                return Task.FromResult<IReadOnlyList<string>>(connections.ToList());
            }

            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    /// <summary>
    /// 获取所有在线用户
    /// </summary>
    /// <returns></returns>
    public Task<IReadOnlyList<string>> GetOnlineUsersAsync()
    {
        lock (_connectionLock)
        {
            return Task.FromResult<IReadOnlyList<string>>(_userConnections.Keys.ToList());
        }
    }

    /// <summary>
    /// 检查用户是否在线
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns></returns>
    public Task<bool> IsUserOnlineAsync(string userId)
    {
        lock (_connectionLock)
        {
            return Task.FromResult(_userConnections.ContainsKey(userId));
        }
    }

    /// <summary>
    /// 获取在线用户数量
    /// </summary>
    /// <returns></returns>
    public Task<int> GetOnlineUserCountAsync()
    {
        lock (_connectionLock)
        {
            return Task.FromResult(_userConnections.Count);
        }
    }
}
