// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 可注入故障的权限存储
/// </summary>
/// <remarks>
/// 读路径直接委托给内存实现，写路径可切换为抛异常，用于验证授权服务把存储异常吞成失败结果的分支。
/// </remarks>
public sealed class FaultInjectingPermissionStore : IPermissionStore
{
    private readonly DefaultPermissionStore _inner = new();

    /// <summary>
    /// 写操作是否抛异常
    /// </summary>
    public bool ThrowOnWrite { get; set; }

    /// <summary>
    /// 内存实现，供用例预置数据
    /// </summary>
    public DefaultPermissionStore Inner => _inner;

    /// <summary>
    /// 获取用户权限列表
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    public Task<List<PermissionDefinition>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _inner.GetUserPermissionsAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 获取角色权限列表
    /// </summary>
    /// <param name="roleId">角色标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    public Task<List<PermissionDefinition>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        return _inner.GetRolePermissionsAsync(roleId, cancellationToken);
    }

    /// <summary>
    /// 授予用户权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task GrantPermissionToUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.GrantPermissionToUserAsync(userId, permissionName, cancellationToken);
    }

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task RevokePermissionFromUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.RevokePermissionFromUserAsync(userId, permissionName, cancellationToken);
    }

    /// <summary>
    /// 授予角色权限
    /// </summary>
    /// <param name="roleId">角色标识</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task GrantPermissionToRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.GrantPermissionToRoleAsync(roleId, permissionName, cancellationToken);
    }

    /// <summary>
    /// 撤销角色权限
    /// </summary>
    /// <param name="roleId">角色标识</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task RevokePermissionFromRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulted();
        return _inner.RevokePermissionFromRoleAsync(roleId, permissionName, cancellationToken);
    }

    /// <summary>
    /// 获取所有权限定义
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    public Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return _inner.GetAllPermissionsAsync(cancellationToken);
    }

    /// <summary>
    /// 按名称获取权限定义
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义</returns>
    public Task<PermissionDefinition?> GetPermissionByNameAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        return _inner.GetPermissionByNameAsync(permissionName, cancellationToken);
    }

    private void ThrowIfFaulted()
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("权限存储写入失败");
        }
    }
}
