#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarEntityTypeHelper
// Guid:2d2f4a74-9e86-4c92-8f5b-258245b0e36d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 22:50:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Helpers;

/// <summary>
/// SqlSugar 实体类型特征辅助类（带缓存）
/// </summary>
internal static class SqlSugarEntityTypeHelper
{
    private static readonly ConcurrentDictionary<Type, EntityTypeMetadata> EntityTypeCache = new();

    /// <summary>
    /// 是否多租户实体
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public static bool IsMultiTenantEntity<TEntity>()
    {
        return GetMetadata(typeof(TEntity)).IsMultiTenantEntity;
    }

    /// <summary>
    /// 是否软删除实体
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public static bool IsSoftDeleteEntity<TEntity>()
    {
        return GetMetadata(typeof(TEntity)).IsSoftDeleteEntity;
    }

    /// <summary>
    /// 是否分表实体
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public static bool IsSplitTableEntity<TEntity>()
    {
        return GetMetadata(typeof(TEntity)).IsSplitTableEntity;
    }

    /// <summary>
    /// 是否多租户实体
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public static bool IsMultiTenantEntity(Type entityType)
    {
        return GetMetadata(entityType).IsMultiTenantEntity;
    }

    /// <summary>
    /// 是否软删除实体
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public static bool IsSoftDeleteEntity(Type entityType)
    {
        return GetMetadata(entityType).IsSoftDeleteEntity;
    }

    /// <summary>
    /// 是否分表实体
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public static bool IsSplitTableEntity(Type entityType)
    {
        return GetMetadata(entityType).IsSplitTableEntity;
    }

    private static EntityTypeMetadata GetMetadata(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return EntityTypeCache.GetOrAdd(entityType, static type => new EntityTypeMetadata(
            typeof(IMultiTenantEntity).IsAssignableFrom(type),
            typeof(ISoftDelete).IsAssignableFrom(type),
            typeof(ISplitTableEntity).IsAssignableFrom(type) ||
            type.GetCustomAttributes(typeof(SplitTableAttribute), inherit: true).Length > 0));
    }

    private readonly record struct EntityTypeMetadata(
        bool IsMultiTenantEntity,
        bool IsSoftDeleteEntity,
        bool IsSplitTableEntity);
}
