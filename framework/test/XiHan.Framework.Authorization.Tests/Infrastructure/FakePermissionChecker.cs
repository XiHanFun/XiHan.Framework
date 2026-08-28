// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Permissions;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 权限检查器替身
/// </summary>
/// <remarks>
/// 既作为固定结果的桩，也记录被检查过的权限编码，用于验证调用方是否真的做了短路。
/// </remarks>
public sealed class FakePermissionChecker : IPermissionChecker
{
    private readonly HashSet<string> _granted;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="grantedPermissions">已授予的权限编码</param>
    public FakePermissionChecker(params string[] grantedPermissions)
    {
        _granted = new HashSet<string>(grantedPermissions, StringComparer.Ordinal);
    }

    /// <summary>
    /// 被 <see cref="IsGrantedAsync"/> 检查过的权限编码，按调用顺序记录
    /// </summary>
    public List<string> CheckedPermissions { get; } = [];

    /// <summary>
    /// 视为“已定义”的权限编码集合，供 <see cref="PermissionExistsAsync"/> 使用
    /// </summary>
    public HashSet<string> ExistingPermissions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有权限</returns>
    public Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        CheckedPermissions.Add(permissionName);
        return Task.FromResult(_granted.Contains(permissionName));
    }

    /// <summary>
    /// 检查是否有任意一个权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有任意一个权限</returns>
    public Task<bool> IsAnyGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(permissionNames.Any(name => _granted.Contains(name)));
    }

    /// <summary>
    /// 检查是否有全部权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有全部权限</returns>
    public Task<bool> IsAllGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(permissionNames.Count > 0 && permissionNames.All(name => _granted.Contains(name)));
    }

    /// <summary>
    /// 获取用户已授予的权限
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限名称列表</returns>
    public Task<List<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_granted.ToList());
    }

    /// <summary>
    /// 检查权限定义是否存在
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingPermissions.Contains(permissionName));
    }
}
