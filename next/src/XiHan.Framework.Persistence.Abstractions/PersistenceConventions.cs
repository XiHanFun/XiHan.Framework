// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// Provider 共用的协议约定。
/// </summary>
/// <remarks>
/// 按实体实现的接口写入创建时间、修改时间、租户、并发戳，并执行软删除与租户匹配。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceConventions
{
    /// <summary>
    /// 新增前。
    /// </summary>
    /// <remarks>
    /// 写入创建时间、租户与并发戳。多租户实体没有当前租户时返回 <see cref="PersistenceErrors.TenantRequired"/>。
    /// </remarks>
    public static Result ApplyOnAdd<TEntity>(TEntity entity, string? tenantId, DateTimeOffset now)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        // 租户校验放在最前面：失败时实体保持原样，不会只写入了一半审计字段
        if (entity is IMultiTenant tenant)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return Error.Validation(PersistenceErrors.TenantRequired, PersistenceMessages.TenantRequired, MessageArguments.Of(("entity", typeof(TEntity).Name)));
            }

            tenant.TenantId = tenantId;
        }

        if (entity is IHasCreationTime creation)
        {
            creation.CreatedAt = now;
        }

        if (entity is IHasConcurrencyStamp stamp)
        {
            stamp.ConcurrencyStamp = NewStamp();
        }

        return Result.Success();
    }

    /// <summary>
    /// 更新前：写入修改时间并刷新并发戳。
    /// </summary>
    public static void ApplyOnUpdate<TEntity>(TEntity entity, DateTimeOffset now)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is IHasModificationTime modification)
        {
            modification.ModifiedAt = now;
        }

        if (entity is IHasConcurrencyStamp stamp)
        {
            stamp.ConcurrencyStamp = NewStamp();
        }
    }

    /// <summary>
    /// 删除：实现 <see cref="ISoftDeletable"/> 时打标并返回 <see langword="true"/>，否则返回 <see langword="false"/> 表示应物理删除。
    /// </summary>
    public static bool TrySoftDelete<TEntity>(TEntity entity, DateTimeOffset now)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not ISoftDeletable deletable)
        {
            return false;
        }

        deletable.IsDeleted = true;
        deletable.DeletedAt = now;
        return true;
    }

    /// <summary>
    /// 实体是否对当前租户可见。
    /// </summary>
    /// <remarks>
    /// 非多租户实体总是可见；多租户实体要求租户相同。
    /// </remarks>
    public static bool IsVisibleTo<TEntity>(TEntity entity, string? tenantId)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        // 没有当前租户（null）的调用看不到任何多租户实体：新增时已保证 TenantId 非空，所以 null 永远不相等
        return entity is not IMultiTenant tenant || string.Equals(tenant.TenantId, tenantId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 实体是否已软删除。
    /// </summary>
    public static bool IsDeleted<TEntity>(TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity is ISoftDeletable { IsDeleted: true };
    }

    /// <summary>
    /// 新的并发戳。
    /// </summary>
    public static string NewStamp() => Guid.NewGuid().ToString("N");
}
