#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SoftDeleteEntityBase
// Guid:pqr12345-1234-1234-1234-123456789pqr
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 软删除实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SoftDeleteEntityBase<TKey> : EntityBase<TKey>, ISoftDelete
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SoftDeleteEntityBase()
    {
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected SoftDeleteEntityBase(TKey id) : base(id)
    {
        IsDeleted = false;
    }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 软删除
    /// </summary>
    public virtual void SoftDelete()
    {
        IsDeleted = true;
    }

    /// <summary>
    /// 恢复删除
    /// </summary>
    public virtual void Restore()
    {
        IsDeleted = false;
    }
}

/// <summary>
/// 删除审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class DeletionEntityBase<TKey> : SoftDeleteEntityBase<TKey>, IDeletionEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DeletionEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected DeletionEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset? DeletionTime { get; set; }

    /// <summary>
    /// 软删除（重写以记录删除时间）
    /// </summary>
    public override void SoftDelete()
    {
        base.SoftDelete();
        DeletionTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// 恢复删除（重写以清除删除时间）
    /// </summary>
    public override void Restore()
    {
        base.Restore();
        DeletionTime = null;
    }
}

/// <summary>
/// 删除审计实体基类（带删除者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public abstract class DeletionEntityBase<TKey, TUserKey> : DeletionEntityBase<TKey>, IDeletionEntity<TUserKey>
    where TKey : IEquatable<TKey>
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DeletionEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected DeletionEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 删除者ID
    /// </summary>
    public virtual TUserKey? DeleterId { get; set; }

    /// <summary>
    /// 软删除（记录删除者）
    /// </summary>
    /// <param name="deleterId">删除者ID</param>
    public virtual void SoftDelete(TUserKey deleterId)
    {
        base.SoftDelete();
        DeleterId = deleterId;
    }

    /// <summary>
    /// 恢复删除（清除删除者）
    /// </summary>
    public override void Restore()
    {
        base.Restore();
        DeleterId = default;
    }
}
