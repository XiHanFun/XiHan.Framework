// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.ExceptionServices;
using SqlSugar;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.SqlSugar.Entities;
using XiHan.Framework.Authorization.SqlSugar.Mapping;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Authorization.SqlSugar.Roles;

/// <summary>
/// 角色存储的 SqlSugar 实现
/// </summary>
/// <remarks>
/// 角色与用户角色关联按当前租户读写，平台态对应租户标识 0。
/// </remarks>
public class SqlSugarRoleStore : IRoleStore
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
    public SqlSugarRoleStore(
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
    /// 获取用户的角色列表
    /// </summary>
    /// <remarks>
    /// 含已禁用的角色，按排序值与名称排列。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public async Task<List<RoleDefinition>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var tenantId = CurrentTenantId;
        var entities = await Client.Queryable<SysAuthzUserRole>()
            .InnerJoin<SysAuthzRole>((member, role) => member.TenantId == role.TenantId && member.RoleId == role.RoleId)
            .Where((member, role) => member.TenantId == tenantId && member.UserId == userId)
            .Select((member, role) => role)
            .ToListAsync(cancellationToken);

        return ToOrderedDefinitions(entities);
    }

    /// <summary>
    /// 检查用户是否在指定角色中
    /// </summary>
    /// <remarks>
    /// 不检查角色是否启用。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否在角色中</returns>
    public async Task<bool> IsInRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return false;
        }

        var tenantId = CurrentTenantId;

        return await Client.Queryable<SysAuthzUserRole>()
            .InnerJoin<SysAuthzRole>((member, role) => member.TenantId == role.TenantId && member.RoleId == role.RoleId)
            .Where((member, role) => member.TenantId == tenantId && member.UserId == userId && role.RoleName == roleName)
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// 将用户添加到角色
    /// </summary>
    /// <remarks>
    /// 已在角色中时不重复写入；关联记录保存角色标识。
    /// </remarks>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="InvalidOperationException">角色不存在</exception>
    public async Task AddUserToRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return;
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var role = await client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleName == roleName, cancellationToken)
            ?? throw new InvalidOperationException($"角色 '{roleName}' 不存在");

        var joined = await client.Queryable<SysAuthzUserRole>()
            .AnyAsync(member => member.TenantId == tenantId && member.UserId == userId && member.RoleId == role.RoleId, cancellationToken);

        if (joined)
        {
            return;
        }

        var entity = new SysAuthzUserRole(_idGenerator.NextId())
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = role.RoleId
        };

        await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 从角色中移除用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RemoveUserFromRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
        {
            return;
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var role = await client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleName == roleName, cancellationToken);

        if (role is null)
        {
            return;
        }

        await client.Deleteable<SysAuthzUserRole>()
            .Where(member => member.TenantId == tenantId && member.UserId == userId && member.RoleId == role.RoleId)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 获取当前租户的所有角色
    /// </summary>
    /// <remarks>
    /// 按排序值升序、同排序值按名称序数升序返回。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    public async Task<List<RoleDefinition>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId;
        var entities = await Client.Queryable<SysAuthzRole>()
            .Where(item => item.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return ToOrderedDefinitions(entities);
    }

    /// <summary>
    /// 根据名称获取角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义，不存在时返回空</returns>
    public async Task<RoleDefinition?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            return null;
        }

        var tenantId = CurrentTenantId;
        var entity = await Client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleName == roleName, cancellationToken);

        return entity is null ? null : RoleMapper.ToDefinition(entity);
    }

    /// <summary>
    /// 根据ID获取角色
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色定义，不存在时返回空</returns>
    public async Task<RoleDefinition?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return null;
        }

        var tenantId = CurrentTenantId;
        var entity = await Client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleId == roleId, cancellationToken);

        return entity is null ? null : RoleMapper.ToDefinition(entity);
    }

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">角色定义为空</exception>
    /// <exception cref="ArgumentException">角色ID或名称为空</exception>
    /// <exception cref="InvalidOperationException">角色ID或名称已存在</exception>
    public async Task CreateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrEmpty(role.Id))
        {
            throw new ArgumentException("角色ID不能为空", nameof(role));
        }

        if (string.IsNullOrEmpty(role.Name))
        {
            throw new ArgumentException("角色名称不能为空", nameof(role));
        }

        var client = Client;
        var tenantId = CurrentTenantId;

        if (await client.Queryable<SysAuthzRole>().AnyAsync(item => item.TenantId == tenantId && item.RoleId == role.Id, cancellationToken))
        {
            throw new InvalidOperationException($"角色ID '{role.Id}' 已存在");
        }

        if (await client.Queryable<SysAuthzRole>().AnyAsync(item => item.TenantId == tenantId && item.RoleName == role.Name, cancellationToken))
        {
            throw new InvalidOperationException($"角色名称 '{role.Name}' 已存在");
        }

        var entity = RoleMapper.ToEntity(role, _idGenerator.NextId());
        entity.TenantId = tenantId;

        await client.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <remarks>
    /// 按角色ID匹配，改写角色ID与创建时间以外的全部字段，并把传入对象的最后修改时间设为当前 UTC 时间。
    /// </remarks>
    /// <param name="role">角色定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">角色定义为空</exception>
    /// <exception cref="ArgumentException">角色ID为空</exception>
    /// <exception cref="InvalidOperationException">角色不存在，或新名称已被其他角色使用</exception>
    public async Task UpdateRoleAsync(RoleDefinition role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrEmpty(role.Id))
        {
            throw new ArgumentException("角色ID不能为空", nameof(role));
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var existing = await client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleId == role.Id, cancellationToken)
            ?? throw new InvalidOperationException($"角色ID '{role.Id}' 不存在");

        if (!string.Equals(existing.RoleName, role.Name, StringComparison.Ordinal))
        {
            var taken = await client.Queryable<SysAuthzRole>()
                .AnyAsync(item => item.TenantId == tenantId && item.RoleName == role.Name && item.BasicId != existing.BasicId, cancellationToken);

            if (taken)
            {
                throw new InvalidOperationException($"角色名称 '{role.Name}' 已被其他角色使用");
            }
        }

        role.LastModifiedTime = DateTime.UtcNow;
        var updated = RoleMapper.ToEntity(role, existing.BasicId);

        await client.Updateable<SysAuthzRole>()
            .SetColumns(item => new SysAuthzRole
            {
                RoleName = updated.RoleName,
                DisplayName = updated.DisplayName,
                Description = updated.Description,
                IsEnabled = updated.IsEnabled,
                IsDefault = updated.IsDefault,
                IsStatic = updated.IsStatic,
                SortOrder = updated.SortOrder,
                LastModifiedTime = updated.LastModifiedTime,
                Properties = updated.Properties
            })
            .Where(item => item.TenantId == tenantId && item.BasicId == existing.BasicId)
            .ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <remarks>
    /// 在同一事务内依次删除该角色的用户关联、角色权限与角色本身；当前已在事务中时并入该事务。
    /// </remarks>
    /// <param name="roleId">角色ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId))
        {
            return;
        }

        var client = Client;
        var tenantId = CurrentTenantId;
        var role = await client.Queryable<SysAuthzRole>()
            .FirstAsync(item => item.TenantId == tenantId && item.RoleId == roleId, cancellationToken);

        if (role is null)
        {
            return;
        }

        await ExecuteInTransactionAsync(client, async () =>
        {
            await client.Deleteable<SysAuthzUserRole>()
                .Where(member => member.TenantId == role.TenantId && member.RoleId == role.RoleId)
                .ExecuteCommandAsync(cancellationToken);

            await client.Deleteable<SysAuthzRolePermission>()
                .Where(grant => grant.TenantId == role.TenantId && grant.RoleId == role.RoleId)
                .ExecuteCommandAsync(cancellationToken);

            await client.Deleteable<SysAuthzRole>()
                .Where(item => item.TenantId == tenantId && item.BasicId == role.BasicId)
                .ExecuteCommandAsync(cancellationToken);
        });
    }

    /// <summary>
    /// 获取角色中的用户ID列表
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>去重后的用户ID列表</returns>
    public async Task<List<string>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            return [];
        }

        var tenantId = CurrentTenantId;
        var userIds = await Client.Queryable<SysAuthzRole>()
            .InnerJoin<SysAuthzUserRole>((role, member) => member.TenantId == role.TenantId && member.RoleId == role.RoleId)
            .Where((role, member) => role.TenantId == tenantId && role.RoleName == roleName)
            .Select((role, member) => member.UserId)
            .ToListAsync(cancellationToken);

        return [.. userIds.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// 按排序值与名称排列并映射为角色定义
    /// </summary>
    /// <param name="entities">角色实体</param>
    /// <returns>角色定义列表</returns>
    private static List<RoleDefinition> ToOrderedDefinitions(List<SysAuthzRole> entities)
    {
        return
        [
            .. entities
                .OrderBy(entity => entity.SortOrder)
                .ThenBy(entity => entity.RoleName, StringComparer.Ordinal)
                .Select(RoleMapper.ToDefinition)
        ];
    }

    /// <summary>
    /// 在事务内执行；已在事务中时直接执行
    /// </summary>
    /// <param name="client">客户端</param>
    /// <param name="action">要执行的操作</param>
    private static async Task ExecuteInTransactionAsync(ISqlSugarClient client, Func<Task> action)
    {
        if (!client.Ado.IsNoTran())
        {
            await action();
            return;
        }

        var result = await client.Ado.UseTranAsync(action);

        if (!result.IsSuccess)
        {
            ExceptionDispatchInfo.Capture(result.ErrorException).Throw();
        }
    }
}
