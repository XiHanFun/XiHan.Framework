#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPermissionChecker
// Guid:d6e7f8a9-b0c1-2345-9012-123456789025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Permissions;

/// <summary>
/// 权限检查器接口
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有权限</returns>
    Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有任意一个权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有任意一个权限</returns>
    Task<bool> IsAnyGrantedAsync(string userId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有所有权限</returns>
    Task<bool> IsAllGrantedAsync(string userId, IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    Task<IEnumerable<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查权限是否存在
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default);
}
