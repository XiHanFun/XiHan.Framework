// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization;

/// <summary>
/// 授权服务接口
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// 检查是否授权
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AuthorizeAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否授权（基于策略）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="policyName">策略名称</param>
    /// <param name="resource">资源对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AuthorizePolicyAsync(string userId, string policyName, object? resource = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否在角色中
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AuthorizeRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有任意权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AuthorizeAnyAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AuthorizeAllAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    Task<List<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 授予用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> GrantPermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> RevokePermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>授权结果</returns>
    Task<AuthorizationResult> RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
}
