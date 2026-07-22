// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 修改审计实体基类
/// </summary>
public abstract class ModificationEntityBase : IModificationEntity
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase()
    {
    }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModifiedTime { get; set; }
}

/// <summary>
/// 修改审计实体基类（带修改者和主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class ModificationEntityBase<TKey> : EntityBase<TKey>, IModificationEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected ModificationEntityBase() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected ModificationEntityBase(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="modifiedId">修改者ID</param>
    protected ModificationEntityBase(TKey basicId, TKey modifiedId) : base(basicId)
    {
        ModifiedId = modifiedId;
    }

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
}
