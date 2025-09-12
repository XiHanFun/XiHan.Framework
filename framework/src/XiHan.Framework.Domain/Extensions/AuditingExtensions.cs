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
    public static void SetCreationAuditInfo(this ICreationEntity entity, DateTimeOffset? creationTime = null)
    {
        entity.CreationTime = creationTime ?? DateTimeOffset.Now;
    }

    /// <summary>
    /// 设置创建审计信息（带创建者）
    /// </summary>
    /// <typeparam name="TUserKey">用户主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="creatorId">创建者ID</param>
    /// <param name="creationTime">创建时间</param>
    public static void SetCreationAuditInfo<TUserKey>(
        this ICreationEntity<TUserKey> entity,
        TUserKey? creatorId,
        DateTimeOffset? creationTime = null)
        where TUserKey : IEquatable<TUserKey>
    {
        entity.CreationTime = creationTime ?? DateTimeOffset.Now;
        entity.CreatorId = creatorId;
    }

    /// <summary>
    /// 设置修改审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="modificationTime">修改时间</param>
    public static void SetModificationAuditInfo(this IModificationEntity entity, DateTimeOffset? modificationTime = null)
    {
        entity.ModificationTime = modificationTime ?? DateTimeOffset.Now;
    }

    /// <summary>
    /// 设置修改审计信息（带修改者）
    /// </summary>
    /// <typeparam name="TUserKey">用户主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="modifierId">修改者ID</param>
    /// <param name="modificationTime">修改时间</param>
    public static void SetModificationAuditInfo<TUserKey>(
        this IModificationEntity<TUserKey> entity,
        TUserKey? modifierId,
        DateTimeOffset? modificationTime = null)
        where TUserKey : IEquatable<TUserKey>
    {
        entity.ModificationTime = modificationTime ?? DateTimeOffset.Now;
        entity.ModifierId = modifierId;
    }

    /// <summary>
    /// 设置删除审计信息
    /// </summary>
    /// <param name="entity">实体</param>
    /// <param name="deletionTime">删除时间</param>
    public static void SetDeletionAuditInfo(this IDeletionEntity entity, DateTimeOffset? deletionTime = null)
    {
        entity.IsDeleted = true;
        entity.DeletionTime = deletionTime ?? DateTimeOffset.Now;
    }

    /// <summary>
    /// 设置删除审计信息（带删除者）
    /// </summary>
    /// <typeparam name="TUserKey">用户主键类型</typeparam>
    /// <param name="entity">实体</param>
    /// <param name="deleterId">删除者ID</param>
    /// <param name="deletionTime">删除时间</param>
    public static void SetDeletionAuditInfo<TUserKey>(
        this IDeletionEntity<TUserKey> entity,
        TUserKey? deleterId,
        DateTimeOffset? deletionTime = null)
        where TUserKey : IEquatable<TUserKey>
    {
        entity.IsDeleted = true;
        entity.DeletionTime = deletionTime ?? DateTimeOffset.Now;
        entity.DeleterId = deleterId;
    }

    /// <summary>
    /// 清除删除审计信息（恢复实体）
    /// </summary>
    /// <param name="entity">实体</param>
    public static void ClearDeletionAuditInfo(this IDeletionEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletionTime = null;
    }

    /// <summary>
    /// 清除删除审计信息（恢复实体，带删除者）
    /// </summary>
    /// <typeparam name="TUserKey">用户主键类型</typeparam>
    /// <param name="entity">实体</param>
    public static void ClearDeletionAuditInfo<TUserKey>(this IDeletionEntity<TUserKey> entity)
        where TUserKey : IEquatable<TUserKey>
    {
        entity.IsDeleted = false;
        entity.DeletionTime = null;
        entity.DeleterId = default;
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
        return DateTimeOffset.Now - entity.CreationTime <= timeSpan;
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
               DateTimeOffset.Now - entity.ModificationTime.Value <= timeSpan;
    }

    /// <summary>
    /// 获取实体的年龄（从创建到现在的时间）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>实体年龄</returns>
    public static TimeSpan GetAge(this ICreationEntity entity)
    {
        return DateTimeOffset.Now - entity.CreationTime;
    }
}
