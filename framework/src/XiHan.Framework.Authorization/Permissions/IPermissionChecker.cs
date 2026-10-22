// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    Task<bool> IsAnyGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有所有权限</returns>
    Task<bool> IsAllGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    Task<List<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查权限是否存在
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 带请求上下文的权限判定（授权处理器调用）
    /// </summary>
    /// <remarks>
    /// 缺省实现转调 <see cref="IsGrantedAsync"/>，不看上下文；需要按请求求值附加条件的实现覆写它，
    /// 并用 <see cref="PermissionCheckResult.ConditionNotMet"/> 区分「条件不满足」与「没有授予」。
    /// </remarks>
    /// <param name="context">判定输入</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>判定结果</returns>
    async Task<PermissionCheckResult> CheckAsync(PermissionCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await IsGrantedAsync(context.UserId, context.PermissionCode, cancellationToken)
            ? PermissionCheckResult.Granted
            : PermissionCheckResult.NotGranted;
    }
}
