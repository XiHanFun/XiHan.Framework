// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Roles;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 可注入故障的角色存储
/// </summary>
/// <remarks>
/// 读路径直接委托给内存实现，用户与角色的关联写路径可切换为抛异常，
/// 用于覆盖授权服务里“角色存在但写入失败”这条与“角色不存在”不同的分支。
/// </remarks>
public sealed class FaultInjectingRoleStore : IRoleStore
{
    private readonly DefaultRoleStore _inner = new();

    /// <summary>
    /// 用户与角色关联的写操作是否抛异常
    /// </summary>
    public bool ThrowOnMembershipWrite { get; set; }

    /// <summary>
    /// 内存实现，供用例预置数据
    /// </summary>
    public DefaultRoleStore Inner => _inner;

    /// <summary>
    /// 获取用户角色列表
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义列表</returns>
    public Task<List<RoleDefinition>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _inner.GetUserRolesAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 检查用户是否在角色中
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否在角色中</returns>
    public Task<bool> IsInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        return _inner.IsInRoleAsync(userId, roleName, cancellationToken);
    }

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.AddUserToRoleAsync(userId, roleName, cancellationToken);
    }

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.RemoveUserFromRoleAsync(userId, roleName, cancellationToken);
    }

    /// <summary>
    /// 获取所有角色
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义列表</returns>
    public Task<List<RoleDefinition>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetAllRolesAsync(cancellationToken);
    }

    /// <summary>
    /// 按名称获取角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    public Task<RoleDefinition?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return _inner.GetRoleByNameAsync(roleName, cancellationToken);
    }

    /// <summary>
    /// 按标识获取角色
    /// </summary>
    /// <param name="roleId">角色标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义</returns>
    public Task<RoleDefinition?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return _inner.GetRoleByIdAsync(roleId, cancellationToken);
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        return _inner.CreateRoleAsync(role, cancellationToken);
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        return _inner.UpdateRoleAsync(role, cancellationToken);
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="roleId">角色标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return _inner.DeleteRoleAsync(roleId, cancellationToken);
    }

    /// <summary>
    /// 获取角色中的用户标识列表
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户标识列表</returns>
    public Task<List<string>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return _inner.GetUsersInRoleAsync(roleName, cancellationToken);
    }

    private void ThrowIfFaulted()
    {
        if (ThrowOnMembershipWrite)
        {
            throw new InvalidOperationException("角色存储写入失败");
        }
    }
}
