#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuditEvent
// Guid:f7c619a2-06a7-41e2-a09d-eb83444216a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/24 6:28:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 审计事件基类，用于描述实体在生命周期内的关键操作
/// </summary>
public abstract class AuditEvent
{
    /// <summary>
    /// 创建审计事件
    /// </summary>
    /// <param name="entity">发生审计的实体实例</param>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="entityId">实体标识</param>
    protected AuditEvent(object entity, string entityType, string entityId)
    {
        Entity = entity;
        EntityType = entityType;
        EntityId = entityId;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 审计的实体对象
    /// </summary>
    public object Entity { get; }

    /// <summary>
    /// 实体类型名称
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// 实体唯一标识
    /// </summary>
    public string EntityId { get; }

    /// <summary>
    /// 事件时间戳（UTC）
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// 实体创建事件
/// </summary>
public class EntityCreatedEvent : AuditEvent
{
    /// <summary>
    /// 创建实体创建事件
    /// </summary>
    /// <param name="entity">被创建的实体</param>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="entityId">实体标识</param>
    public EntityCreatedEvent(object entity, string entityType, string entityId)
        : base(entity, entityType, entityId)
    {
    }
}

/// <summary>
/// 实体修改事件
/// </summary>
public class EntityModifiedEvent : AuditEvent
{
    /// <summary>
    /// 创建实体修改事件
    /// </summary>
    /// <param name="entity">修改后的实体</param>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="entityId">实体标识</param>
    /// <param name="originalEntity">原始实体</param>
    public EntityModifiedEvent(object entity, string entityType, string entityId, object? originalEntity = null)
        : base(entity, entityType, entityId)
    {
        OriginalEntity = originalEntity;
    }

    /// <summary>
    /// 原始实体
    /// </summary>
    public object? OriginalEntity { get; }
}

/// <summary>
/// 实体删除事件
/// </summary>
public class EntityDeletedEvent : AuditEvent
{
    /// <summary>
    /// 创建实体删除事件
    /// </summary>
    /// <param name="entity">被删除的实体</param>
    /// <param name="entityType">实体类型名称</param>
    /// <param name="entityId">实体标识</param>
    public EntityDeletedEvent(object entity, string entityType, string entityId)
        : base(entity, entityType, entityId)
    {
    }
}
