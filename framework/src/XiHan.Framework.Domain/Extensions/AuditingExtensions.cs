#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuditingExtensions
// Guid:vwx12345-1234-1234-1234-123456789vwx
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:44:00
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
    /// <param name="creationTime">创建时间</param>
    /// <returns>创建审计实体</returns>
    public static ICreationEntity SetCreationAuditInfo(this ICreationEntity entity, DateTimeOffset? creationTime = null)
    {
        entity.CreationTime = creationTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置创建审计信息（带创建者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="creatorId">创建者Id</param>
    /// <param name="creator">创建人</param>
    /// <param name="creationTime">创建时间</param>
    /// <returns>创建审计实体</returns>
    public static ICreationEntity<TKey> SetCreationAuditInfo<TKey>(
        this ICreationEntity<TKey> entity,
        TKey? creatorId,
        string? creator = null,
        DateTimeOffset? creationTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.CreationTime = creationTime ?? DateTimeOffset.UtcNow;
        entity.CreatorId = creatorId;
        entity.Creator = creator;
        return entity;
    }

    /// <summary>
    /// 批量设置创建审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <param name="creatorId">创建者Id</param>
    /// <param name="creator">创建人</param>
    /// <param name="creationTime">创建时间</param>
    /// <returns>创建审计实体集合</returns>
    public static IEnumerable<ICreationEntity<TKey>> SetCreationAuditInfos<TKey>(
        this IEnumerable<ICreationEntity<TKey>> entities,
        TKey? creatorId,
        string? creator = null,
        DateTimeOffset? creationTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entities)
        {
            entity.CreationTime = creationTime ?? DateTimeOffset.UtcNow;
            entity.CreatorId = creatorId;
            entity.Creator = creator;
        }
        return entities;
    }

    /// <summary>
    /// 设置修改审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="modificationTime">修改时间</param>
    /// <returns>修改审计实体</returns>
    public static IModificationEntity SetModificationAuditInfo(this IModificationEntity entity, DateTimeOffset? modificationTime = null)
    {
        entity.ModificationTime = modificationTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置修改审计信息（带修改者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="modifierId">修改者Id</param>
    /// <param name="modifier">修改人</param>
    /// <param name="modificationTime">修改时间</param>
    /// <returns>修改审计实体</returns>
    public static IModificationEntity<TKey> SetModificationAuditInfo<TKey>(
        this IModificationEntity<TKey> entity,
        TKey? modifierId,
        string? modifier = null,
        DateTimeOffset? modificationTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.ModificationTime = modificationTime ?? DateTimeOffset.UtcNow;
        entity.ModifierId = modifierId;
        entity.Modifier = modifier;
        return entity;
    }

    /// <summary>
    /// 批量设置修改审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <param name="modifierId">修改者Id</param>
    /// <param name="modifier">修改人</param>
    /// <param name="modificationTime">修改时间</param>
    /// <returns>修改审计实体集合</returns>
    public static IEnumerable<IModificationEntity<TKey>> SetModificationAuditInfos<TKey>(
        this IEnumerable<IModificationEntity<TKey>> entities,
        TKey? modifierId,
        string? modifier = null,
        DateTimeOffset? modificationTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entities)
        {
            entity.ModificationTime = modificationTime ?? DateTimeOffset.UtcNow;
            entity.ModifierId = modifierId;
            entity.Modifier = modifier;
        }
        return entities;
    }

    /// <summary>
    /// 设置删除审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="deletionTime">删除时间</param>
    /// <returns>删除审计实体</returns>
    public static IDeletionEntity SetDeletionAuditInfo(this IDeletionEntity entity, DateTimeOffset? deletionTime = null)
    {
        entity.IsDeleted = true;
        entity.DeletionTime = deletionTime ?? DateTimeOffset.UtcNow;
        return entity;
    }

    /// <summary>
    /// 设置删除审计信息（带删除者）
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="deleterId">删除者Id</param>
    /// <param name="deleter">删除人</param>
    /// <param name="deletionTime">删除时间</param>
    public static IDeletionEntity<TKey> SetDeletionAuditInfo<TKey>(
        this IDeletionEntity<TKey> entity,
        TKey? deleterId,
        string? deleter = null,
        DateTimeOffset? deletionTime = null)
        where TKey : IEquatable<TKey>
    {
        entity.IsDeleted = true;
        entity.DeletionTime = deletionTime ?? DateTimeOffset.UtcNow;
        entity.DeleterId = deleterId;
        entity.Deleter = deleter;
        return entity;
    }

    /// <summary>
    /// 批量删除审计信息
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <param name="entitys">实体集合</param>
    /// <param name="deleterId">删除者Id</param>
    /// <param name="deleter">删除人</param>
    /// <param name="deletionTime">删除时间</param>
    /// <returns>删除审计实体集合</returns>
    public static IEnumerable<IDeletionEntity<TKey>> SetDeletionAuditInfos<TKey>(
        this IEnumerable<IDeletionEntity<TKey>> entitys,
        TKey? deleterId,
        string? deleter = null,
        DateTimeOffset? deletionTime = null)
        where TKey : IEquatable<TKey>
    {
        foreach (var entity in entitys)
        {
            entity.IsDeleted = true;
            entity.DeletionTime = deletionTime ?? DateTimeOffset.UtcNow;
            entity.DeleterId = deleterId;
            entity.Deleter = deleter;
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
        entity.DeletionTime = null;
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
        entity.DeletionTime = null;
        entity.DeleterId = default;
        entity.Deleter = null;
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
            entity.DeletionTime = null;
            entity.DeleterId = default;
            entity.Deleter = null;
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
        return DateTimeOffset.UtcNow - entity.CreationTime <= timeSpan;
    }

    /// <summary>
    /// 获取实体的年龄（从创建到现在的时间）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>实体年龄</returns>
    public static TimeSpan GetAge(this ICreationEntity entity)
    {
        return DateTimeOffset.UtcNow - entity.CreationTime;
    }

    /// <summary>
    /// 检查实体是否最近被修改（在指定时间内）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="timeSpan">时间间隔</param>
    /// <returns>如果最近被修改返回 true，否则返回 false</returns>
    public static bool IsRecentlyModified(this IModificationEntity entity, TimeSpan timeSpan)
    {
        return entity.ModificationTime.HasValue &&
               DateTimeOffset.UtcNow - entity.ModificationTime.Value <= timeSpan;
    }

    /// <summary>
    /// 获取实体距离最后修改的时间
    /// </summary>
    public static TimeSpan? GetTimeSinceLastModification(this IModificationEntity entity)
    {
        return entity.ModificationTime.HasValue
            ? DateTimeOffset.UtcNow - entity.ModificationTime.Value
            : null;
    }

    /// <summary>
    /// 检查实体是否从未被修改过
    /// </summary>
    public static bool IsNeverModified(this IModificationEntity entity)
    {
        return !entity.ModificationTime.HasValue;
    }

    /// <summary>
    /// 获取审计摘要信息
    /// </summary>
    public static string GetAuditSummary<TKey>(this IFullAuditedEntity<TKey> entity)
        where TKey : IEquatable<TKey>
    {
        var summary = $"Created: {entity.CreationTime:yyyy-MM-dd HH:mm:ss}";

        if (!string.IsNullOrEmpty(entity.Creator))
        {
            summary += $" by {entity.Creator}";
        }

        if (entity.ModificationTime.HasValue)
        {
            summary += $", Modified: {entity.ModificationTime.Value:yyyy-MM-dd HH:mm:ss}";
            if (!string.IsNullOrEmpty(entity.Modifier))
            {
                summary += $" by {entity.Modifier}";
            }
        }

        if (entity.IsDeleted && entity.DeletionTime.HasValue)
        {
            summary += $", Deleted: {entity.DeletionTime.Value:yyyy-MM-dd HH:mm:ss}";
            if (!string.IsNullOrEmpty(entity.Deleter))
            {
                summary += $" by {entity.Deleter}";
            }
        }

        return summary;
    }
}
