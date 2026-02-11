#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultRoleManager
// Guid:a5b6c7d8-e9f0-1234-5678-123456789042
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Roles;

/// <summary>
/// 默认角色管理器
/// </summary>
public class DefaultRoleManager : IRoleManager
{
    private readonly IRoleStore _roleStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="roleStore">角色存储</param>
    public DefaultRoleManager(IRoleStore roleStore)
    {
        _roleStore = roleStore;
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<RoleOperationResult> CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        try
        {
            // 验证角色数据
            var validationResult = ValidateRole(role);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            // 检查角色是否已存在
            var existingRole = await _roleStore.GetRoleByNameAsync(role.Name, cancellationToken);
            if (existingRole != null)
            {
                return RoleOperationResult.Failure($"角色名称 '{role.Name}' 已存在", "ROLE_NAME_DUPLICATE");
            }

            // 生成角色ID（如果未提供）
            if (string.IsNullOrEmpty(role.Id))
            {
                role.Id = Guid.NewGuid().ToString();
            }

            role.CreatedTime = DateTime.UtcNow;

            await _roleStore.CreateRoleAsync(role, cancellationToken);
            return RoleOperationResult.Success(role);
        }
        catch (Exception ex)
        {
            return RoleOperationResult.Failure($"创建角色失败: {ex.Message}", "CREATE_FAILED");
        }
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<RoleOperationResult> UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        try
        {
            // 验证角色数据
            var validationResult = ValidateRole(role);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            // 检查角色是否存在
            var existingRole = await _roleStore.GetRoleByIdAsync(role.Id, cancellationToken);
            if (existingRole == null)
            {
                return RoleOperationResult.Failure($"角色ID '{role.Id}' 不存在", "ROLE_NOT_FOUND");
            }

            // 检查是否为静态角色
            if (existingRole.IsStatic)
            {
                return RoleOperationResult.Failure($"静态角色 '{existingRole.Name}' 不允许修改", "STATIC_ROLE_READONLY");
            }

            // 如果名称改变了，检查新名称是否已被使用
            if (existingRole.Name != role.Name)
            {
                var roleWithSameName = await _roleStore.GetRoleByNameAsync(role.Name, cancellationToken);
                if (roleWithSameName != null && roleWithSameName.Id != role.Id)
                {
                    return RoleOperationResult.Failure($"角色名称 '{role.Name}' 已被其他角色使用", "ROLE_NAME_DUPLICATE");
                }
            }

            role.LastModifiedTime = DateTime.UtcNow;

            await _roleStore.UpdateRoleAsync(role, cancellationToken);
            return RoleOperationResult.Success(role);
        }
        catch (Exception ex)
        {
            return RoleOperationResult.Failure($"更新角色失败: {ex.Message}", "UPDATE_FAILED");
        }
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<RoleOperationResult> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(roleId))
            {
                return RoleOperationResult.Failure("角色ID不能为空", "INVALID_ROLE_ID");
            }

            // 检查角色是否存在
            var role = await _roleStore.GetRoleByIdAsync(roleId, cancellationToken);
            if (role == null)
            {
                return RoleOperationResult.Failure($"角色ID '{roleId}' 不存在", "ROLE_NOT_FOUND");
            }

            // 检查是否为静态角色
            if (role.IsStatic)
            {
                return RoleOperationResult.Failure($"静态角色 '{role.Name}' 不允许删除", "STATIC_ROLE_READONLY");
            }

            await _roleStore.DeleteRoleAsync(roleId, cancellationToken);
            return RoleOperationResult.Success();
        }
        catch (Exception ex)
        {
            return RoleOperationResult.Failure($"删除角色失败: {ex.Message}", "DELETE_FAILED");
        }
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public async Task<List<RoleDefinition>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _roleStore.GetAllRolesAsync(cancellationToken);
    }

    /// <summary>
    /// 根据名称获取角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    public async Task<RoleDefinition?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await _roleStore.GetRoleByNameAsync(roleName, cancellationToken);
    }

    /// <summary>
    /// 检查角色是否存在
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            return false;
        }

        var role = await _roleStore.GetRoleByNameAsync(roleName, cancellationToken);
        return role != null;
    }

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<RoleOperationResult> AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return RoleOperationResult.Failure("用户ID不能为空", "INVALID_USER_ID");
            }

            if (string.IsNullOrEmpty(roleName))
            {
                return RoleOperationResult.Failure("角色名称不能为空", "INVALID_ROLE_NAME");
            }

            // 检查角色是否存在
            var role = await _roleStore.GetRoleByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return RoleOperationResult.Failure($"角色 '{roleName}' 不存在", "ROLE_NOT_FOUND");
            }

            // 检查角色是否启用
            if (!role.IsEnabled)
            {
                return RoleOperationResult.Failure($"角色 '{roleName}' 已禁用", "ROLE_DISABLED");
            }

            // 检查用户是否已在角色中
            var isInRole = await _roleStore.IsInRoleAsync(userId, roleName, cancellationToken);
            if (isInRole)
            {
                return RoleOperationResult.Failure($"用户已在角色 '{roleName}' 中", "USER_ALREADY_IN_ROLE");
            }

            await _roleStore.AddUserToRoleAsync(userId, roleName, cancellationToken);
            return RoleOperationResult.Success(role);
        }
        catch (Exception ex)
        {
            return RoleOperationResult.Failure($"添加用户到角色失败: {ex.Message}", "ADD_USER_FAILED");
        }
    }

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<RoleOperationResult> RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return RoleOperationResult.Failure("用户ID不能为空", "INVALID_USER_ID");
            }

            if (string.IsNullOrEmpty(roleName))
            {
                return RoleOperationResult.Failure("角色名称不能为空", "INVALID_ROLE_NAME");
            }

            // 检查角色是否存在
            var role = await _roleStore.GetRoleByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return RoleOperationResult.Failure($"角色 '{roleName}' 不存在", "ROLE_NOT_FOUND");
            }

            // 检查用户是否在角色中
            var isInRole = await _roleStore.IsInRoleAsync(userId, roleName, cancellationToken);
            if (!isInRole)
            {
                return RoleOperationResult.Failure($"用户不在角色 '{roleName}' 中", "USER_NOT_IN_ROLE");
            }

            await _roleStore.RemoveUserFromRoleAsync(userId, roleName, cancellationToken);
            return RoleOperationResult.Success(role);
        }
        catch (Exception ex)
        {
            return RoleOperationResult.Failure($"从角色中移除用户失败: {ex.Message}", "REMOVE_USER_FAILED");
        }
    }

    /// <summary>
    /// 获取用户的所有角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public async Task<List<RoleDefinition>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _roleStore.GetUserRolesAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 检查用户是否在指定角色中
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否在角色中</returns>
    public async Task<bool> IsUserInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        return await _roleStore.IsInRoleAsync(userId, roleName, cancellationToken);
    }

    /// <summary>
    /// 验证角色数据
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <returns>验证结果</returns>
    private static RoleOperationResult ValidateRole(RoleDefinition role)
    {
        if (role == null)
        {
            return RoleOperationResult.Failure("角色数据不能为空", "INVALID_ROLE");
        }

        if (string.IsNullOrWhiteSpace(role.Name))
        {
            return RoleOperationResult.Failure("角色名称不能为空", "INVALID_ROLE_NAME");
        }

        if (string.IsNullOrWhiteSpace(role.DisplayName))
        {
            return RoleOperationResult.Failure("角色显示名称不能为空", "INVALID_DISPLAY_NAME");
        }

        // 角色名称不能包含特殊字符
        if (role.Name.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
        {
            return RoleOperationResult.Failure("角色名称只能包含字母、数字、下划线和连字符", "INVALID_ROLE_NAME_FORMAT");
        }

        return RoleOperationResult.Success();
    }
}
