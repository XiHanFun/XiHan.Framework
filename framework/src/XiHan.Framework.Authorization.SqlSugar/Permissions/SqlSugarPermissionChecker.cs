// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authorization.SqlSugar.Permissions;

/// <summary>
/// 权限检查器的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 直接查询授权表：一次判定先查用户在当前租户直接授予的启用权限，未全部命中时再查经启用角色授予的启用权限。
/// 平台态对应租户标识 0。
/// </remarks>
public class SqlSugarPermissionChecker : IPermissionChecker
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    public SqlSugarPermissionChecker(ISqlSugarClientResolver clientResolver, ICurrentTenant currentTenant)
    {
        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 当前租户主库的客户端
    /// </summary>
    private ISqlSugarClient Client => _clientResolver.GetCurrentClient();

    /// <summary>
    /// 当前租户标识，平台态为 0
    /// </summary>
    private long CurrentTenantId => _currentTenant.Id ?? 0;

    /// <summary>
    /// 检查是否有指定权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有权限</returns>
    public async Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return false;
        }

        var granted = await GetGrantedNamesAsync(userId, [permissionName], cancellationToken);

        return granted.Contains(permissionName);
    }

    /// <summary>
    /// 检查是否有任意一个权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有任意一个权限</returns>
    public async Task<bool> IsAnyGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var names = permissionNames.ToList();
        if (names.Count == 0 || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var candidates = ToCandidates(names);
        if (candidates.Count == 0)
        {
            return false;
        }

        var granted = await GetGrantedNamesAsync(userId, candidates, cancellationToken);

        return names.Any(granted.Contains);
    }

    /// <summary>
    /// 检查是否有所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionNames">权限名称列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否有所有权限；列表为空或含空名称时返回 false</returns>
    public async Task<bool> IsAllGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
    {
        var names = permissionNames.ToList();
        if (names.Count == 0 || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var candidates = ToCandidates(names);
        if (candidates.Count == 0)
        {
            return false;
        }

        var granted = await GetGrantedNamesAsync(userId, candidates, cancellationToken);

        return names.All(granted.Contains);
    }

    /// <summary>
    /// 获取用户的所有权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>直接授予与经启用角色授予的启用权限名称，已去重</returns>
    public async Task<List<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var granted = await GetGrantedNamesAsync(userId, null, cancellationToken);

        return [.. granted];
    }

    /// <summary>
    /// 检查权限是否存在
    /// </summary>
    /// <remarks>
    /// 有权限定义即视为存在，不检查启用状态。
    /// </remarks>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return false;
        }

        return await Client.Queryable<SysAuthzPermission>()
            .AnyAsync(permission => permission.PermissionName == permissionName, cancellationToken);
    }

    /// <summary>
    /// 去掉空名称并去重
    /// </summary>
    /// <param name="names">权限名称</param>
    /// <returns>候选权限名称</returns>
    private static List<string> ToCandidates(List<string> names)
    {
        return [.. names.Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// 查询用户已被授予的启用权限名称
    /// </summary>
    /// <remarks>
    /// 先查直接授予；候选不为空且已全部命中时直接返回，否则再查经启用角色授予的部分。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="candidates">只查这些权限，为空表示不限</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已授予的权限名称</returns>
    private async Task<HashSet<string>> GetGrantedNamesAsync(string userId, List<string>? candidates, CancellationToken cancellationToken)
    {
        var client = Client;
        var tenantId = CurrentTenantId;

        var directQuery = client.Queryable<SysAuthzUserPermission>()
            .InnerJoin<SysAuthzPermission>((grant, permission) => permission.PermissionName == grant.PermissionName)
            .Where((grant, permission) => grant.TenantId == tenantId && grant.UserId == userId && permission.IsEnabled);

        var roleQuery = client.Queryable<SysAuthzUserRole>()
            .InnerJoin<SysAuthzRole>((member, role) => role.TenantId == member.TenantId && role.RoleId == member.RoleId)
            .InnerJoin<SysAuthzRolePermission>((member, role, grant) => grant.TenantId == role.TenantId && grant.RoleId == role.RoleId)
            .InnerJoin<SysAuthzPermission>((member, role, grant, permission) => permission.PermissionName == grant.PermissionName)
            .Where((member, role, grant, permission) => member.TenantId == tenantId && member.UserId == userId && role.IsEnabled && permission.IsEnabled);

        if (candidates is not null)
        {
            List<string> names = candidates;

            directQuery = directQuery.Where((grant, permission) => names.Contains(permission.PermissionName));
            roleQuery = roleQuery.Where((member, role, grant, permission) => names.Contains(permission.PermissionName));
        }

        var directNames = await directQuery
            .Select((grant, permission) => permission.PermissionName)
            .ToListAsync(cancellationToken);

        var granted = new HashSet<string>(directNames, StringComparer.Ordinal);

        if (candidates is not null && candidates.All(granted.Contains))
        {
            return granted;
        }

        var roleNames = await roleQuery
            .Select((member, role, grant, permission) => permission.PermissionName)
            .ToListAsync(cancellationToken);

        granted.UnionWith(roleNames);

        return granted;
    }
}
