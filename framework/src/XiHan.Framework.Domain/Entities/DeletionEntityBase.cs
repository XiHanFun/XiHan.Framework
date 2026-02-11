#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DeletionEntityBase
// Guid:7634e970-1d6c-482e-8e85-20f28561eb22
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 软删除实体基类
/// </summary>
public abstract class SoftDeleteEntityBase : ISoftDelete
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SoftDeleteEntityBase()
    {
        IsDeleted = false;
    }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }
}

/// <summary>
/// 删除审计实体基类
/// </summary>
public abstract class DeletionEntityBase : SoftDeleteEntityBase, IDeletionEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DeletionEntityBase() : base()
    {
    }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset? DeletedTime { get; set; }
}

/// <summary>
/// 删除审计实体基类（带删除者和主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class DeletionEntityBase<TKey> : EntityBase<TKey>, IDeletionEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DeletionEntityBase() : base()
    {
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected DeletionEntityBase(TKey basicId) : base(basicId)
    {
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="deletedId">删除者ID</param>
    protected DeletionEntityBase(TKey basicId, TKey deletedId) : base(basicId)
    {
        IsDeleted = false;
        DeletedId = deletedId;
    }

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
