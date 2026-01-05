#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRoleStore
// Guid:b0c1d2e3-f4a5-6789-3456-123456789029
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 角色存储接口
/// </summary>
/// <remarks>
/// 用户需要实现此接口来提供角色数据的存储和检索功能
/// </remarks>
public interface IRoleStore
{
    /// <summary>
    /// 获取用户的角色列表
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
    Task<bool> IsInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

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
    /// 根据ID获取角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    Task<RoleDefinition?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取角色中的用户ID列表
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户ID列表</returns>
    Task<IEnumerable<string>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);
}
