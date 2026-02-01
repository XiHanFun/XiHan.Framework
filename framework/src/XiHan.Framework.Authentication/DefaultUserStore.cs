#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultUserStore
// Guid:a9b0c1d2-e3f4-5678-2345-123456789028
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 默认用户存储实现
/// </summary>
/// <remarks>
/// 基于内存的用户存储实现，适用于开发、测试环境或作为参考实现。
/// 生产环境建议实现基于数据库的存储。
/// </remarks>
public class DefaultUserStore : IUserStore
{
    /// <summary>
    /// 用户字典（用户ID -> 用户信息）
    /// </summary>
    private readonly ConcurrentDictionary<string, UserInfo> _usersById = new();

    /// <summary>
    /// 用户名到ID的映射（用户名 -> 用户ID）
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _usernameToId = new();

    /// <summary>
    /// 用于线程安全操作的锁对象
    /// </summary>
    private readonly Lock _lockObject = new();

    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    public Task<UserInfo?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Task.FromResult<UserInfo?>(null);
        }

        if (!_usernameToId.TryGetValue(username, out var userId))
        {
            return Task.FromResult<UserInfo?>(null);
        }

        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    public Task<UserInfo?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<UserInfo?>(null);
        }

        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    public Task UpdateUserAsync(UserInfo user, CancellationToken cancellationToken = default)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
        {
            throw new ArgumentException("用户信息或用户ID不能为空", nameof(user));
        }

        lock (_lockObject)
        {
            if (!_usersById.ContainsKey(user.UserId))
            {
                throw new InvalidOperationException($"用户 {user.UserId} 不存在");
            }

            _usersById[user.UserId] = user;

            // 如果用户名改变了，更新映射
            var oldUsername = _usernameToId.FirstOrDefault(x => x.Value == user.UserId).Key;
            if (oldUsername != null && oldUsername != user.Username)
            {
                _usernameToId.TryRemove(oldUsername, out _);
                _usernameToId.TryAdd(user.Username, user.UserId);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新用户密码
    /// </summary>
    public async Task UpdatePasswordAsync(string userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("用户ID和密码哈希不能为空");
        }

        var user = await GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"用户 {userId} 不存在");
        }

        user.PasswordHash = passwordHash;
        await UpdateUserAsync(user, cancellationToken);
    }

    /// <summary>
    /// 获取登录失败次数
    /// </summary>
    public async Task<int> GetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByUsernameAsync(username, cancellationToken);
        return user?.FailedLoginAttempts ?? 0;
    }

    /// <summary>
    /// 记录登录失败
    /// </summary>
    public async Task IncrementFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByUsernameAsync(username, cancellationToken);
        if (user != null)
        {
            user.FailedLoginAttempts++;
            await UpdateUserAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 重置登录失败次数
    /// </summary>
    public async Task ResetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByUsernameAsync(username, cancellationToken);
        if (user != null)
        {
            user.FailedLoginAttempts = 0;
            await UpdateUserAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 设置账户锁定时间
    /// </summary>
    public async Task SetLockoutEndAsync(string username, DateTime? lockoutEnd, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByUsernameAsync(username, cancellationToken);
        if (user != null)
        {
            user.LockoutEnd = lockoutEnd;
            user.IsLocked = lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow;
            await UpdateUserAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// 获取账户锁定结束时间
    /// </summary>
    public async Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByUsernameAsync(username, cancellationToken);
        return user?.LockoutEnd;
    }

    /// <summary>
    /// 添加用户
    /// </summary>
    /// <param name="user">用户信息</param>
    public Task AddUserAsync(UserInfo user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            throw new ArgumentException("用户ID不能为空", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Username))
        {
            throw new ArgumentException("用户名不能为空", nameof(user));
        }

        lock (_lockObject)
        {
            if (_usersById.ContainsKey(user.UserId))
            {
                throw new InvalidOperationException($"用户ID {user.UserId} 已存在");
            }

            if (_usernameToId.ContainsKey(user.Username))
            {
                throw new InvalidOperationException($"用户名 {user.Username} 已存在");
            }

            _usersById.TryAdd(user.UserId, user);
            _usernameToId.TryAdd(user.Username, user.UserId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    public Task DeleteUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            if (_usersById.TryRemove(userId, out var user))
            {
                _usernameToId.TryRemove(user.Username, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 清空所有用户
    /// </summary>
    public Task ClearAsync()
    {
        _usersById.Clear();
        _usernameToId.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有用户
    /// </summary>
    public Task<IEnumerable<UserInfo>> GetAllUsersAsync()
    {
        var users = _usersById.Values.AsEnumerable();
        return Task.FromResult(users);
    }

    /// <summary>
    /// 批量添加用户
    /// </summary>
    /// <param name="users">用户列表</param>
    public Task AddUsersAsync(IEnumerable<UserInfo> users)
    {
        if (users == null)
        {
            return Task.CompletedTask;
        }

        foreach (var user in users.Where(u => u != null && !string.IsNullOrWhiteSpace(u.UserId) && !string.IsNullOrWhiteSpace(u.Username)))
        {
            lock (_lockObject)
            {
                if (!_usersById.ContainsKey(user.UserId) && !_usernameToId.ContainsKey(user.Username))
                {
                    _usersById.TryAdd(user.UserId, user);
                    _usernameToId.TryAdd(user.Username, user.UserId);
                }
            }
        }

        return Task.CompletedTask;
    }
}
