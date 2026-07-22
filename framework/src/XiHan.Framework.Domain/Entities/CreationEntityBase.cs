// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 创建审计实体基类
/// </summary>
public abstract class CreationEntityBase : ICreationEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CreationEntityBase()
    {
        CreatedTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset CreatedTime { get; set; }
}

/// <summary>
/// 创建审计实体基类（带创建者和主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class CreationEntityBase<TKey> : EntityBase<TKey>, ICreationEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CreationEntityBase() : base()
    {
        CreatedTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected CreationEntityBase(TKey basicId) : base(basicId)
    {
        CreatedTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="createdId">创建者ID</param>
    protected CreationEntityBase(TKey basicId, TKey createdId) : base(basicId)
    {
        CreatedTime = DateTimeOffset.UtcNow;
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
}
