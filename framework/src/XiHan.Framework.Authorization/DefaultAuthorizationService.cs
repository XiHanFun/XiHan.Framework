#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultAuthorizationService
// Guid:b2c3d4e5-f6a7-8901-5678-123456789041
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization;

/// <summary>
/// 默认授权服务实现
/// </summary>
public class DefaultAuthorizationService : IAuthorizationService
{
    private readonly IPermissionChecker _permissionChecker;
    private readonly IPermissionStore _permissionStore;
    private readonly IRoleStore _roleStore;
    private readonly IPolicyEvaluator _policyEvaluator;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultAuthorizationService(
        IPermissionChecker permissionChecker,
        IPermissionStore permissionStore,
        IRoleStore roleStore,
        IPolicyEvaluator policyEvaluator)
    {
        _permissionChecker = permissionChecker;
        _permissionStore = permissionStore;
        _roleStore = roleStore;
        _policyEvaluator = policyEvaluator;
    }

    /// <summary>
    /// 检查是否授权
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        var isGranted = await _permissionChecker.IsGrantedAsync(userId, permissionName, cancellationToken);
        return isGranted
            ? AuthorizationResult.Success()
            : AuthorizationResult.PermissionDenied(permissionName);
    }

    /// <summary>
    /// 检查是否授权（基于策略）
    /// </summary>
    public async Task<AuthorizationResult> AuthorizePolicyAsync(string userId, string policyName, object? resource = null, CancellationToken cancellationToken = default)
    {
        var policyResult = await _policyEvaluator.EvaluateAsync(userId, policyName, resource, cancellationToken);

        return policyResult.Succeeded
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failure(policyResult.FailureReason ?? "策略评估失败", policyResult.FailedRequirements);
    }

    /// <summary>
    /// 检查是否在角色中
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        var isInRole = await _roleStore.IsInRoleAsync(userId, roleName, cancellationToken);
        return isInRole
            ? AuthorizationResult.Success()
            : AuthorizationResult.RoleDenied(roleName);
    }

    /// <summary>
    /// 检查是否有任意权限
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeAnyAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var permissions = permissionNames.ToList();
        var isGranted = await _permissionChecker.IsAnyGrantedAsync(userId, permissions, cancellationToken);

        return isGranted
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failure("缺少所需权限（任意一个）", permissions);
    }

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    public async Task<AuthorizationResult> AuthorizeAllAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var permissions = permissionNames.ToList();
        var isGranted = await _permissionChecker.IsAllGrantedAsync(userId, permissions, cancellationToken);

        return isGranted
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failure("缺少所需权限（全部）", permissions);
    }

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    public async Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _permissionChecker.GetGrantedPermissionsAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    public async Task<List<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleStore.GetUserRolesAsync(userId, cancellationToken);
        return roles.Select(r => r.Name).ToList();
    }

    /// <summary>
    /// 授予用户权限
    /// </summary>
    public async Task<AuthorizationResult> GrantPermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查权限是否存在
            var permissionExists = await _permissionChecker.PermissionExistsAsync(permissionName, cancellationToken);
            if (!permissionExists)
            {
                return AuthorizationResult.Failure($"权限不存在: {permissionName}");
            }

            await _permissionStore.GrantPermissionToUserAsync(userId, permissionName, cancellationToken);
            return AuthorizationResult.Success();
        }
        catch (Exception ex)
        {
            return AuthorizationResult.Failure($"授予权限失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    public async Task<AuthorizationResult> RevokePermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _permissionStore.RevokePermissionFromUserAsync(userId, permissionName, cancellationToken);
            return AuthorizationResult.Success();
        }
        catch (Exception ex)
        {
            return AuthorizationResult.Failure($"撤销权限失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    public async Task<AuthorizationResult> AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查角色是否存在
            var role = await _roleStore.GetRoleByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return AuthorizationResult.Failure($"角色不存在: {roleName}");
            }

            await _roleStore.AddUserToRoleAsync(userId, roleName, cancellationToken);
            return AuthorizationResult.Success();
        }
        catch (Exception ex)
        {
            return AuthorizationResult.Failure($"添加用户到角色失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    public async Task<AuthorizationResult> RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _roleStore.RemoveUserFromRoleAsync(userId, roleName, cancellationToken);
            return AuthorizationResult.Success();
        }
        catch (Exception ex)
        {
            return AuthorizationResult.Failure($"从角色中移除用户失败: {ex.Message}");
        }
    }
}
