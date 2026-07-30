// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
