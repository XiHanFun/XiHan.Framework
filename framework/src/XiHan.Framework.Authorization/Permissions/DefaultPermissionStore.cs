#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultPermissionStore
// Guid:b1c2d3e4-f5a6-7890-1234-123456789029
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 默认权限存储实现
/// </summary>
/// <remarks>
/// 基于内存的权限存储实现，适用于开发、测试环境或作为参考实现。
/// 生产环境建议实现基于数据库的存储。
/// </remarks>
public class DefaultPermissionStore : IPermissionStore
{
    /// <summary>
    /// 所有权限定义（权限名称 -> 权限定义）
    /// </summary>
    private readonly ConcurrentDictionary<string, PermissionDefinition> _permissions = new();

    /// <summary>
    /// 用户权限（用户ID -> 权限名称集合）
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _userPermissions = new();

    /// <summary>
    /// 角色权限（角色ID -> 权限名称集合）
    /// </summary>
    private readonly ConcurrentDictionary<string, HashSet<string>> _rolePermissions = new();

    /// <summary>
    /// 用于线程安全操作的锁对象
    /// </summary>
    private readonly Lock _lockObject = new();

    /// <summary>
    /// 获取用户的权限列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    public Task<List<PermissionDefinition>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(Enumerable.Empty<PermissionDefinition>().ToList());
        }

        if (!_userPermissions.TryGetValue(userId, out var permissionNames))
        {
            return Task.FromResult(Enumerable.Empty<PermissionDefinition>().ToList());
        }

        var permissions = permissionNames
            .Select(name => _permissions.TryGetValue(name, out var permission) ? permission : null)
            .Where(p => p != null)
            .Cast<PermissionDefinition>();

        return Task.FromResult(permissions.ToList());
    }

    /// <summary>
    /// 获取角色的权限列表
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    public Task<List<PermissionDefinition>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return Task.FromResult(Enumerable.Empty<PermissionDefinition>().ToList());
        }

        if (!_rolePermissions.TryGetValue(roleId, out var permissionNames))
        {
            return Task.FromResult(Enumerable.Empty<PermissionDefinition>().ToList());
        }

        var permissions = permissionNames
            .Select(name => _permissions.TryGetValue(name, out var permission) ? permission : null)
            .Where(p => p != null)
            .Cast<PermissionDefinition>();

        return Task.FromResult(permissions.ToList());
    }

    /// <summary>
    /// 授予用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task GrantPermissionToUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            var permissions = _userPermissions.GetOrAdd(userId, _ => new HashSet<string>());
            permissions.Add(permissionName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RevokePermissionFromUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return Task.CompletedTask;
        }

        if (_userPermissions.TryGetValue(userId, out var permissions))
        {
            lock (_lockObject)
            {
                permissions.Remove(permissionName);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 授予角色权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task GrantPermissionToRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(permissionName))
        {
            return Task.CompletedTask;
        }

        lock (_lockObject)
        {
            var permissions = _rolePermissions.GetOrAdd(roleId, _ => new HashSet<string>());
            permissions.Add(permissionName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 撤销角色权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task RevokePermissionFromRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(permissionName))
        {
            return Task.CompletedTask;
        }

        if (_rolePermissions.TryGetValue(roleId, out var permissions))
        {
            lock (_lockObject)
            {
                permissions.Remove(permissionName);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取所有权限定义
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    public Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = _permissions.Values.AsEnumerable();
        return Task.FromResult(permissions.ToList());
    }

    /// <summary>
    /// 根据名称获取权限定义
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义</returns>
    public Task<PermissionDefinition?> GetPermissionByNameAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return Task.FromResult<PermissionDefinition?>(null);
        }

        _permissions.TryGetValue(permissionName, out var permission);
        return Task.FromResult(permission);
    }

    /// <summary>
    /// 添加或更新权限定义
    /// </summary>
    /// <param name="permission">权限定义</param>
    /// <returns>是否成功</returns>
    public Task<bool> AddOrUpdatePermissionAsync(PermissionDefinition permission)
    {
        if (permission == null || string.IsNullOrEmpty(permission.Name))
        {
            return Task.FromResult(false);
        }

        _permissions.AddOrUpdate(permission.Name, permission, (_, _) => permission);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 删除权限定义
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <returns>是否成功</returns>
    public Task<bool> RemovePermissionAsync(string permissionName)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return Task.FromResult(false);
        }

        var result = _permissions.TryRemove(permissionName, out _);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 清空所有权限数据
    /// </summary>
    public Task ClearAsync()
    {
        _permissions.Clear();
        _userPermissions.Clear();
        _rolePermissions.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量添加权限定义
    /// </summary>
    /// <param name="permissions">权限定义列表</param>
    public Task AddPermissionsAsync(List<PermissionDefinition> permissions)
    {
        if (permissions == null)
        {
            return Task.CompletedTask;
        }

        foreach (var permission in permissions.Where(p => p != null && !string.IsNullOrEmpty(p.Name)))
        {
            _permissions.AddOrUpdate(permission.Name, permission, (_, _) => permission);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取用户权限名称列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>权限名称列表</returns>
    public Task<List<string>> GetUserPermissionNamesAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        if (!_userPermissions.TryGetValue(userId, out var permissions))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        return Task.FromResult(permissions.ToList());
    }

    /// <summary>
    /// 获取角色权限名称列表
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <returns>权限名称列表</returns>
    public Task<List<string>> GetRolePermissionNamesAsync(string roleId)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        if (!_rolePermissions.TryGetValue(roleId, out var permissions))
        {
            return Task.FromResult(Enumerable.Empty<string>().ToList());
        }

        return Task.FromResult(permissions.ToList());
    }
}
