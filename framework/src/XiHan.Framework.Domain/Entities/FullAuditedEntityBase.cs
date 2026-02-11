#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FullAuditedEntityBase
// Guid:0054f87b-8167-415b-bb23-8c76169c7ef1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 完整审计实体基类
/// 包含创建、修改、删除的所有审计信息
/// </summary>
public abstract class FullAuditedEntityBase : IFullAuditedEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected FullAuditedEntityBase()
    {
        RowVersion = 0;
        CreatedTime = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    public virtual long RowVersion { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModifiedTime { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset? DeletedTime { get; set; }
}

/// <summary>
/// 完整审计实体基类（带用户和主键）
/// 包含创建、修改、删除的所有审计信息和对应的用户唯一标识
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class FullAuditedEntityBase<TKey> : EntityBase<TKey>, IFullAuditedEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected FullAuditedEntityBase() : base()
    {
        CreatedTime = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected FullAuditedEntityBase(TKey basicId) : base(basicId)
    {
        CreatedTime = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="createdId">创建者ID</param>
    protected FullAuditedEntityBase(TKey basicId, TKey createdId) : base(basicId)
    {
        CreatedTime = DateTimeOffset.UtcNow;
        IsDeleted = false;
        CreatedId = createdId;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    public virtual TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string? CreatedBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModifiedTime { get; set; }

    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    public virtual TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    public virtual string? ModifiedBy { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset? DeletedTime { get; set; }

    /// <summary>
    /// 删除者唯一标识
    /// </summary>
    public virtual TKey? DeletedId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    public virtual string? DeletedBy { get; set; }
}
