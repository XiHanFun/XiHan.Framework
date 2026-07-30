// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 权限存储接口
/// </summary>
/// <remarks>
/// 用户需要实现此接口来提供权限数据的存储和检索功能
/// </remarks>
public interface IPermissionStore
{
    /// <summary>
    /// 获取用户的权限列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    Task<List<PermissionDefinition>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取角色的权限列表
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    Task<List<PermissionDefinition>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 授予用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task GrantPermissionToUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RevokePermissionFromUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 授予角色权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task GrantPermissionToRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销角色权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RevokePermissionFromRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有权限定义
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取权限定义
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义</returns>
    Task<PermissionDefinition?> GetPermissionByNameAsync(string permissionName, CancellationToken cancellationToken = default);
}
