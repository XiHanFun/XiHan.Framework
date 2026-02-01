#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuditingExtensions
// Guid:c003e690-1a09-40a7-b5cd-5d3e5a178bee
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:44:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Extensions;

/// <summary>
/// 审计扩展方法
/// </summary>
public static class AuditingExtensions
{
    /// <summary>
    /// 设置创建审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>创建审计实体</returns>
    public static ICreationEntity SetCreationAuditInfo(this ICreationEntity entity, DateTimeOffset? createdTime = null)
    {
        entity.CreatedTime = createdTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置创建审计信息（带创建者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="createdId">创建者唯一标识</param>
    /// <param name="createdBy">创建人</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>创建审计实体</returns>
    public static ICreationEntity<TKey> SetCreationAuditInfo<TKey>(
        this ICreationEntity<TKey> entity,
        TKey? createdId,
        string? createdBy = null,
        DateTimeOffset? createdTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.CreatedTime = createdTime ?? DateTimeOffset.UtcNow;
        entity.CreatedId = createdId;
        entity.CreatedBy = createdBy;
        return entity;
    }

    /// <summary>
    /// 批量设置创建审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <param name="createdId">创建者唯一标识</param>
    /// <param name="createdBy">创建人</param>
    /// <param name="createdTime">创建时间</param>
    /// <returns>创建审计实体集合</returns>
    public static IEnumerable<ICreationEntity<TKey>> SetCreationAuditInfos<TKey>(
        this IEnumerable<ICreationEntity<TKey>> entities,
        TKey? createdId,
        string? createdBy = null,
        DateTimeOffset? createdTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entities)
        {
            entity.CreatedTime = createdTime ?? DateTimeOffset.UtcNow;
            entity.CreatedId = createdId;
            entity.CreatedBy = createdBy;
        }
        return entities;
    }

    /// <summary>
    /// 设置修改审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="modifiedTime">修改时间</param>
    /// <returns>修改审计实体</returns>
    public static IModificationEntity SetModificationAuditInfo(this IModificationEntity entity, DateTimeOffset? modifiedTime = null)
    {
        entity.ModifiedTime = modifiedTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置修改审计信息（带修改者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="modifiedId">修改者唯一标识</param>
    /// <param name="modifiedBy">修改人</param>
    /// <param name="modifiedTime">修改时间</param>
    /// <returns>修改审计实体</returns>
    public static IModificationEntity<TKey> SetModificationAuditInfo<TKey>(
        this IModificationEntity<TKey> entity,
        TKey? modifiedId,
        string? modifiedBy = null,
        DateTimeOffset? modifiedTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.ModifiedTime = modifiedTime ?? DateTimeOffset.UtcNow;
        entity.ModifiedId = modifiedId;
        entity.ModifiedBy = modifiedBy;
        return entity;
    }

    /// <summary>
    /// 批量设置修改审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <param name="modifiedId">修改者唯一标识</param>
    /// <param name="modifiedBy">修改人</param>
    /// <param name="modifiedTime">修改时间</param>
    /// <returns>修改审计实体集合</returns>
    public static IEnumerable<IModificationEntity<TKey>> SetModificationAuditInfos<TKey>(
        this IEnumerable<IModificationEntity<TKey>> entities,
        TKey? modifiedId,
        string? modifiedBy = null,
        DateTimeOffset? modifiedTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entities)
        {
            entity.ModifiedTime = modifiedTime ?? DateTimeOffset.UtcNow;
            entity.ModifiedId = modifiedId;
            entity.ModifiedBy = modifiedBy;
        }
        return entities;
    }

    /// <summary>
    /// 设置删除审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="deletedTime">删除时间</param>
    /// <returns>删除审计实体</returns>
    public static IDeletionEntity SetDeletionAuditInfo(this IDeletionEntity entity, DateTimeOffset? deletedTime = null)
    {
        entity.IsDeleted = true;
        entity.DeletedTime = deletedTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置删除审计信息（带删除者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="deletedId">删除者唯一标识</param>
    /// <param name="deletedBy">删除人</param>
    /// <param name="deletedTime">删除时间</param>
    public static IDeletionEntity<TKey> SetDeletionAuditInfo<TKey>(
        this IDeletionEntity<TKey> entity,
        TKey? deletedId,
        string? deletedBy = null,
        DateTimeOffset? deletedTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.IsDeleted = true;
        entity.DeletedTime = deletedTime ?? DateTimeOffset.UtcNow;
        entity.DeletedId = deletedId;
        entity.DeletedBy = deletedBy;
        return entity;
    }

    /// <summary>
    /// 批量删除审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entitys">实体集合</param>
    /// <param name="deletedId">删除者唯一标识</param>
    /// <param name="deletedBy">删除人</param>
    /// <param name="deletedTime">删除时间</param>
    /// <returns>删除审计实体集合</returns>
    public static IEnumerable<IDeletionEntity<TKey>> SetDeletionAuditInfos<TKey>(
        this IEnumerable<IDeletionEntity<TKey>> entitys,
        TKey? deletedId,
        string? deletedBy = null,
        DateTimeOffset? deletedTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entitys)
        {
            entity.IsDeleted = true;
            entity.DeletedTime = deletedTime ?? DateTimeOffset.UtcNow;
            entity.DeletedId = deletedId;
            entity.DeletedBy = deletedBy;
        }
        return entitys;
    }

    /// <summary>
    /// 清除删除审计信息（恢复实体）
    /// </summary>
    /// <param name="entity">实体</param>
    public static IDeletionEntity ClearDeletionAuditInfo(this IDeletionEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedTime = null;
        return entity;
    }

    /// <summary>
    /// 清除删除审计信息（恢复实体，带删除者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    public static IDeletionEntity<TKey> ClearDeletionAuditInfo<TKey>(this IDeletionEntity<TKey> entity)
        where TKey : IEquatable<TKey>
    {
        entity.IsDeleted = false;
        entity.DeletedTime = null;
        entity.DeletedId = default;
        entity.DeletedBy = null;
        return entity;
    }

    /// <summary>
    /// 批量清除删除审计信息（恢复实体）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entitys">实体集合</param>
    /// <returns>清除删除审计实体集合</returns>
    public static IEnumerable<IDeletionEntity<TKey>> ClearDeletionAuditInfos<TKey>(this IEnumerable<IDeletionEntity<TKey>> entitys)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entitys)
        {
            entity.IsDeleted = false;
            entity.DeletedTime = null;
            entity.DeletedId = default;
            entity.DeletedBy = null;
        }
        return entitys;
    }

    /// <summary>
    /// 检查实体是否已被软删除
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>如果已删除返回 true，否则返回 false</returns>
    public static bool IsSoftDeleted(this ISoftDelete entity)
    {
        return entity.IsDeleted;
    }

    /// <summary>
    /// 检查实体是否为新创建的（在指定时间内）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="timeSpan">时间间隔</param>
    /// <returns>如果是新创建的返回 true，否则返回 false</returns>
    public static bool IsNewlyCreated(this ICreationEntity entity, TimeSpan timeSpan)
    {
        return DateTimeOffset.UtcNow - entity.CreatedTime <= timeSpan;
    }

    /// <summary>
    /// 获取实体的年龄（从创建到现在的时间）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>实体年龄</returns>
    public static TimeSpan GetAge(this ICreationEntity entity)
    {
        return DateTimeOffset.UtcNow - entity.CreatedTime;
    }

    /// <summary>
    /// 检查实体是否最近被修改（在指定时间内）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="timeSpan">时间间隔</param>
    /// <returns>如果最近被修改返回 true，否则返回 false</returns>
    public static bool IsRecentlyModified(this IModificationEntity entity, TimeSpan timeSpan)
    {
        return entity.ModifiedTime.HasValue &&
               DateTimeOffset.UtcNow - entity.ModifiedTime.Value <= timeSpan;
    }

    /// <summary>
    /// 获取实体距离最后修改的时间
    /// </summary>
    public static TimeSpan? GetTimeSinceLastModification(this IModificationEntity entity)
    {
        return entity.ModifiedTime.HasValue
            ? DateTimeOffset.UtcNow - entity.ModifiedTime.Value
            : null;
    }

    /// <summary>
    /// 检查实体是否从未被修改过
    /// </summary>
    public static bool IsNeverModified(this IModificationEntity entity)
    {
        return !entity.ModifiedTime.HasValue;
    }

    /// <summary>
    /// 获取审计摘要信息
    /// </summary>
    public static string GetAuditSummary<TKey>(this IFullAuditedEntity<TKey> entity)
        where TKey : IEquatable<TKey>
    {
        var summary = $"Created: {entity.CreatedTime:yyyy-MM-dd HH:mm:ss}";

        if (!string.IsNullOrEmpty(entity.CreatedBy))
        {
            summary += $" by {entity.CreatedBy}";
        }

        if (entity.ModifiedTime.HasValue)
        {
            summary += $", Modified: {entity.ModifiedTime.Value:yyyy-MM-dd HH:mm:ss}";
            if (!string.IsNullOrEmpty(entity.ModifiedBy))
            {
                summary += $" by {entity.ModifiedBy}";
            }
        }

        if (entity.IsDeleted && entity.DeletedTime.HasValue)
        {
            summary += $", Deleted: {entity.DeletedTime.Value:yyyy-MM-dd HH:mm:ss}";
            if (!string.IsNullOrEmpty(entity.DeletedBy))
            {
                summary += $" by {entity.DeletedBy}";
            }
        }

        return summary;
    }
}
