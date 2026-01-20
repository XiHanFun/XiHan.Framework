#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultPermissionChecker
// Guid:a9b0c1d2-e3f4-5678-2345-123456789028
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 默认权限检查器实现
/// </summary>
public class DefaultPermissionChecker : IPermissionChecker
{
    private readonly IPermissionStore _permissionStore;
    private readonly IRoleStore _roleStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permissionStore">权限存储</param>
    /// <param name="roleStore">角色存储</param>
    public DefaultPermissionChecker(IPermissionStore permissionStore, IRoleStore roleStore)
    {
        _permissionStore = permissionStore;
        _roleStore = roleStore;
    }

    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有权限</returns>
    public async Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return false;
        }

        // 检查用户直接拥有的权限
        var userPermissions = await _permissionStore.GetUserPermissionsAsync(userId, cancellationToken);
        if (userPermissions.Any(p => p.Name == permissionName && p.IsEnabled))
        {
            return true;
        }

        // 检查用户角色的权限
        var userRoles = await _roleStore.GetUserRolesAsync(userId, cancellationToken);
        foreach (var role in userRoles.Where(r => r.IsEnabled))
        {
            var rolePermissions = await _permissionStore.GetRolePermissionsAsync(role.Id, cancellationToken);
            if (rolePermissions.Any(p => p.Name == permissionName && p.IsEnabled))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查是否有任意一个权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有任意一个权限</returns>
    public async Task<bool> IsAnyGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var names = permissionNames.ToList();
        if (!names.Any())
        {
            return false;
        }

        foreach (var permissionName in names)
        {
            if (await IsGrantedAsync(userId, permissionName, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有所有权限</returns>
    public async Task<bool> IsAllGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var names = permissionNames.ToList();
        if (!names.Any())
        {
            return false;
        }

        foreach (var permissionName in names)
        {
            if (!await IsGrantedAsync(userId, permissionName, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    public async Task<List<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var permissions = new HashSet<string>();

        // 获取用户直接拥有的权限
        var userPermissions = await _permissionStore.GetUserPermissionsAsync(userId, cancellationToken);
        foreach (var permission in userPermissions.Where(p => p.IsEnabled))
        {
            permissions.Add(permission.Name);
        }

        // 获取用户角色的权限
        var userRoles = await _roleStore.GetUserRolesAsync(userId, cancellationToken);
        foreach (var role in userRoles.Where(r => r.IsEnabled))
        {
            var rolePermissions = await _permissionStore.GetRolePermissionsAsync(role.Id, cancellationToken);
            foreach (var permission in rolePermissions.Where(p => p.IsEnabled))
            {
                permissions.Add(permission.Name);
            }
        }

        return [.. permissions];
    }

    /// <summary>
    /// 检查权限是否存在
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        var permission = await _permissionStore.GetPermissionByNameAsync(permissionName, cancellationToken);
        return permission != null;
    }
}
