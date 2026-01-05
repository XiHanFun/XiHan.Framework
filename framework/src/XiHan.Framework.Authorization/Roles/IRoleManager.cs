#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRoleManager
// Guid:d2e3f4a5-b6c7-8901-5678-123456789031
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 角色管理器接口
/// </summary>
public interface IRoleManager
{
    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<RoleOperationResult> CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<RoleOperationResult> UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<RoleOperationResult> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有角色
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    Task<IEnumerable<RoleDefinition>> GetAllRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    Task<RoleDefinition?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查角色是否存在
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<RoleOperationResult> AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<RoleOperationResult> RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    Task<IEnumerable<RoleDefinition>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查用户是否在指定角色中
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否在角色中</returns>
    Task<bool> IsUserInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
}
