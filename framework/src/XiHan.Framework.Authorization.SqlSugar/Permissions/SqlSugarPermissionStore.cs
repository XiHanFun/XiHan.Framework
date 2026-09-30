// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authorization.SqlSugar.Permissions;

/// <summary>
/// 权限存储的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 授予记录按当前租户读写，平台态对应租户标识 0；权限定义全局共享。
/// </remarks>
public class SqlSugarPermissionStore : IPermissionStore
{
    private readonly ISqlSugarClientResolver _clientResolver;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedIdGenerator<long> _idGenerator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="clientResolver">客户端解析器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="idGenerator">主键生成器</param>
    public SqlSugarPermissionStore(
        ISqlSugarClientResolver clientResolver,
        ICurrentTenant currentTenant,
        IDistributedIdGenerator<long> idGenerator)
    {
        _clientResolver = clientResolver;
        _currentTenant = currentTenant;
        _idGenerator = idGenerator;
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
    /// 获取用户直接拥有的权限列表
    /// </summary>
    /// <remarks>
    /// 只返回有权限定义的授予，不含经角色得到的权限，不过滤启用状态。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    public async Task<List<PermissionDefinition>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var tenantId = CurrentTenantId;
        var entities = await Client.Queryable<SysAuthzUserPermission>()
            .InnerJoin<SysAuthzPermission>((grant, permission) => grant.PermissionName == permission.PermissionName)
            .Where((grant, permission) => grant.TenantId == tenantId && grant.UserId == userId)
            .Select((grant, permission) => permission)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(PermissionMapper.ToDefinition)];
    }

    /// <summary>
    /// 获取角色的权限列表
    /// </summary>
    /// <remarks>
    /// 只返回有权限定义的授予，不过滤启用状态。
    /// </remarks>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限列表</returns>
    public async Task<List<PermissionDefinition>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return [];
        }

        var tenantId = CurrentTenantId;
        var entities = await Client.Queryable<SysAuthzRolePermission>()
            .InnerJoin<SysAuthzPermission>((grant, permission) => grant.PermissionName == permission.PermissionName)
            .Where((grant, permission) => grant.TenantId == tenantId && grant.RoleId == roleId)
            .Select((grant, permission) => permission)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(PermissionMapper.ToDefinition)];
    }

    /// <summary>
    /// 授予用户权限
    /// </summary>
    /// <remarks>
    /// 已授予时不重复写入；不校验权限定义是否存在。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GrantPermissionToUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return;
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var granted = await client.Queryable<SysAuthzUserPermission>()
            .AnyAsync(grant => grant.TenantId == tenantId && grant.UserId == userId && grant.PermissionName == permissionName, cancellationToken);

        if (granted)
        {
            return;
        }

        var entity = new SysAuthzUserPermission(_idGenerator.NextId())
        {
            TenantId = tenantId,
            UserId = userId,
            PermissionName = permissionName
        };

        await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 撤销用户权限
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RevokePermissionFromUserAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionName))
        {
            return;
        }

        var tenantId = CurrentTenantId;
        await Client.Deleteable<SysAuthzUserPermission>()
            .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && grant.PermissionName == permissionName)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 授予角色权限
    /// </summary>
    /// <remarks>
    /// 已授予时不重复写入；不校验角色与权限定义是否存在。
    /// </remarks>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task GrantPermissionToRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(permissionName))
        {
            return;
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var granted = await client.Queryable<SysAuthzRolePermission>()
            .AnyAsync(grant => grant.TenantId == tenantId && grant.RoleId == roleId && grant.PermissionName == permissionName, cancellationToken);

        if (granted)
        {
            return;
        }

        var entity = new SysAuthzRolePermission(_idGenerator.NextId())
        {
            TenantId = tenantId,
            RoleId = roleId,
            PermissionName = permissionName
        };

        await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 撤销角色权限
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RevokePermissionFromRoleAsync(string roleId, string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(permissionName))
        {
            return;
        }

        var tenantId = CurrentTenantId;
        await Client.Deleteable<SysAuthzRolePermission>()
            .Where(grant => grant.TenantId == tenantId && grant.RoleId == roleId && grant.PermissionName == permissionName)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 获取所有权限定义
    /// </summary>
    /// <remarks>
    /// 按排序值升序、同排序值按名称序数升序返回。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义列表</returns>
    public async Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await Client.Queryable<SysAuthzPermission>().ToListAsync(cancellationToken);

        return
        [
            .. entities
                .OrderBy(entity => entity.SortOrder)
                .ThenBy(entity => entity.PermissionName, StringComparer.Ordinal)
                .Select(PermissionMapper.ToDefinition)
        ];
    }

    /// <summary>
    /// 根据名称获取权限定义
    /// </summary>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>权限定义，不存在时返回空</returns>
    public async Task<PermissionDefinition?> GetPermissionByNameAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return null;
        }

        var entity = await Client.Queryable<SysAuthzPermission>()
            .FirstAsync(permission => permission.PermissionName == permissionName, cancellationToken);

        return entity is null ? null : PermissionMapper.ToDefinition(entity);
    }

    /// <summary>
    /// 添加或更新权限定义
    /// </summary>
    /// <remarks>
    /// 按名称匹配：不存在时新增，存在时改写名称以外的全部字段。
    /// </remarks>
    /// <param name="permission">权限定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定义为空或名称为空时返回 false，否则返回 true</returns>
    public async Task<bool> AddOrUpdatePermissionAsync(PermissionDefinition permission, CancellationToken cancellationToken = default)
    {
        if (permission is null || string.IsNullOrEmpty(permission.Name))
        {
            return false;
        }

        var client = Client;
        var existing = await client.Queryable<SysAuthzPermission>()
            .FirstAsync(entity => entity.PermissionName == permission.Name, cancellationToken);

        if (existing is null)
        {
            await client.Insertable(PermissionMapper.ToEntity(permission, _idGenerator.NextId()))
                .ExecuteCommandAsync(cancellationToken);

            return true;
        }

        var updated = PermissionMapper.ToEntity(permission, existing.BasicId);

        await client.Updateable<SysAuthzPermission>()
            .SetColumns(entity => new SysAuthzPermission
            {
                DisplayName = updated.DisplayName,
                Description = updated.Description,
                ParentName = updated.ParentName,
                Tag = updated.Tag,
                IsEnabled = updated.IsEnabled,
                SortOrder = updated.SortOrder,
                Properties = updated.Properties
            })
            .Where(entity => entity.BasicId == existing.BasicId)
            .ExecuteCommandAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// 批量添加或更新权限定义
    /// </summary>
    /// <remarks>
    /// 跳过空定义与名称为空的定义，其余逐条调用 <see cref="AddOrUpdatePermissionAsync"/>。
    /// </remarks>
    /// <param name="permissions">权限定义列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AddPermissionsAsync(List<PermissionDefinition> permissions, CancellationToken cancellationToken = default)
    {
        if (permissions is null)
        {
            return;
        }

        foreach (var permission in permissions.Where(item => item is not null && !string.IsNullOrEmpty(item.Name)))
        {
            await AddOrUpdatePermissionAsync(permission, cancellationToken);
        }
    }

    /// <summary>
    /// 删除权限定义
    /// </summary>
    /// <remarks>
    /// 只删除定义，不删除已有的授予行。
    /// </remarks>
    /// <param name="permissionName">权限名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除到了定义返回 true</returns>
    public async Task<bool> RemovePermissionAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(permissionName))
        {
            return false;
        }

        var deleted = await Client.Deleteable<SysAuthzPermission>()
            .Where(entity => entity.PermissionName == permissionName)
            .ExecuteCommandAsync(cancellationToken);

        return deleted > 0;
    }
}
