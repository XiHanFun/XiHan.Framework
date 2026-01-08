#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultRoleStore
// Guid:f4a5b6c7-d8e9-0123-4567-123456789041
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 默认角色存储实现
/// </summary>
/// <remarks>
/// 基于内存的角色存储实现，适用于开发、测试环境或作为参考实现。
/// 生产环境建议实现基于数据库的存储。
/// </remarks>
public class DefaultRoleStore : IRoleStore
{
    /// <summary>
    /// 所有角色定义（角色ID -> 角色定义）
    /// </summary>
    private readonly ConcurrentDictionary<string, RoleDefinition> _rolesById = new();

    /// <summary>
    /// 角色名称到ID的映射（角色名称 -> 角色ID）
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _roleNameToId = new();

    /// <summary>
    /// 用户角色关系（用户ID -> 角色名称集合）
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _userRoles = new();

    /// <summary>
    /// 用于线程安全操作的锁对象
    /// </summary>
    private readonly Lock _lockObject = new();

    /// <summary>
    /// 获取用户的角色列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public Task<List<RoleDefinition>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(Enumerable.Empty<RoleDefinition>().ToList());
        }

        if (!_userRoles.TryGetValue(userId, out var roleNames))
        {
            return Task.FromResult(Enumerable.Empty<RoleDefinition>().ToList());
        }

        var roles = roleNames
            .Select(name => _roleNameToId.TryGetValue(name, out var roleId) ? roleId : null)
            .Where(id => id != null)
            .Select(id => _rolesById.TryGetValue(id!, out var role) ? role : null)
            .Where(r => r != null)
            .Cast<RoleDefinition>();

        return Task.FromResult(roles.ToList());
    }

    /// <summary>
    /// 检查用户是否在指定角色中
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否在角色中</returns>
    public Task<bool> IsInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return Task.FromResult(false);
        }

        if (!_userRoles.TryGetValue(userId, out var roleNames))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(roleNames.Contains(roleName));
    }

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return Task.CompletedTask;
        }

        // 检查角色是否存在
        if (!_roleNameToId.ContainsKey(roleName))
        {
            throw new InvalidOperationException($"角色 '{roleName}' 不存在");
        }

        lock (_lockObject)
        {
            var roles = _userRoles.GetOrAdd(userId, _ => new HashSet<string>());
            roles.Add(roleName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return Task.CompletedTask;
        }

        if (_userRoles.TryGetValue(userId, out var roles))
        {
            lock (_lockObject)
            {
                roles.Remove(roleName);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public Task<List<RoleDefinition>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_rolesById.Values.ToList());
    }

    /// <summary>
    /// 根据名称获取角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    public Task<RoleDefinition?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            return Task.FromResult<RoleDefinition?>(null);
        }

        if (!_roleNameToId.TryGetValue(roleName, out var roleId))
        {
            return Task.FromResult<RoleDefinition?>(null);
        }

        _rolesById.TryGetValue(roleId, out var role);
        return Task.FromResult(role);
    }

    /// <summary>
    /// 根据ID获取角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    public Task<RoleDefinition?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return Task.FromResult<RoleDefinition?>(null);
        }

        _rolesById.TryGetValue(roleId, out var role);
        return Task.FromResult(role);
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        if (string.IsNullOrEmpty(role.Id))
        {
            throw new ArgumentException("角色ID不能为空", nameof(role));
        }

        if (string.IsNullOrEmpty(role.Name))
        {
            throw new ArgumentException("角色名称不能为空", nameof(role));
        }

        lock (_lockObject)
        {
            if (_rolesById.ContainsKey(role.Id))
            {
                throw new InvalidOperationException($"角色ID '{role.Id}' 已存在");
            }

            if (_roleNameToId.ContainsKey(role.Name))
            {
                throw new InvalidOperationException($"角色名称 '{role.Name}' 已存在");
            }

            _rolesById.TryAdd(role.Id, role);
            _roleNameToId.TryAdd(role.Name, role.Id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        if (role == null)
        {
            throw new ArgumentNullException(nameof(role));
        }

        if (string.IsNullOrEmpty(role.Id))
        {
            throw new ArgumentException("角色ID不能为空", nameof(role));
        }

        lock (_lockObject)
        {
            if (!_rolesById.TryGetValue(role.Id, out var existingRole))
            {
                throw new InvalidOperationException($"角色ID '{role.Id}' 不存在");
            }

            // 如果角色名称改变了，需要更新名称映射
            if (existingRole.Name != role.Name)
            {
                if (_roleNameToId.ContainsKey(role.Name))
                {
                    throw new InvalidOperationException($"角色名称 '{role.Name}' 已被其他角色使用");
                }

                _roleNameToId.TryRemove(existingRole.Name, out _);
                _roleNameToId.TryAdd(role.Name, role.Id);
            }

            role.LastModifiedTime = DateTime.UtcNow;
            _rolesById[role.Id] = role;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            if (_rolesById.TryRemove(roleId, out var role))
            {
                _roleNameToId.TryRemove(role.Name, out _);

                // 从所有用户中移除此角色
                foreach (var userRoles in _userRoles.Values)
                {
                    userRoles.Remove(role.Name);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取角色中的用户ID列表
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户ID列表</returns>
    public Task<List<string>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        var userIds = _userRoles
            .Where(kvp => kvp.Value.Contains(roleName))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult(userIds);
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public Task ClearAsync()
    {
        _rolesById.Clear();
        _roleNameToId.Clear();
        _userRoles.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量添加角色
    /// </summary>
    /// <param name="roles">角色定义列表</param>
    public Task AddRolesAsync(List<RoleDefinition> roles)
    {
        if (roles == null)
        {
            return Task.CompletedTask;
        }

        foreach (var role in roles.Where(r => r != null && !string.IsNullOrEmpty(r.Id) && !string.IsNullOrEmpty(r.Name)))
        {
            lock (_lockObject)
            {
                if (!_rolesById.ContainsKey(role.Id) && !_roleNameToId.ContainsKey(role.Name))
                {
                    _rolesById.TryAdd(role.Id, role);
                    _roleNameToId.TryAdd(role.Name, role.Id);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取用户角色名称列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>角色名称列表</returns>
    public Task<List<string>> GetUserRoleNamesAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        if (!_userRoles.TryGetValue(userId, out var roleNames))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        return Task.FromResult(roleNames.ToList());
    }
}
