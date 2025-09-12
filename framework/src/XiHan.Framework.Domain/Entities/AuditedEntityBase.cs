#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FullAuditedEntityBase
// Guid:stu12345-1234-1234-1234-123456789stu
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 完整审计实体基类
/// 包含创建、修改、删除的所有审计信息
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class AuditedEntityBase<TKey> : EntityBase<TKey>, IFullAuditedEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected AuditedEntityBase()
    {
        CreationTime = DateTimeOffset.Now;
        IsDeleted = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected AuditedEntityBase(TKey id) : base(id)
    {
        CreationTime = DateTimeOffset.Now;
        IsDeleted = false;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset CreationTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public virtual DateTimeOffset? ModificationTime { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public virtual DateTimeOffset? DeletionTime { get; set; }

    /// <summary>
    /// 软删除
    /// </summary>
    public virtual void SoftDelete()
    {
        IsDeleted = true;
        DeletionTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// 恢复删除
    /// </summary>
    public virtual void Restore()
    {
        IsDeleted = false;
        DeletionTime = null;
    }

    /// <summary>
    /// 标记为已修改
    /// </summary>
    public virtual void MarkAsModified()
    {
        ModificationTime = DateTimeOffset.Now;
    }
}

/// <summary>
/// 完整审计实体基类（带用户）
/// 包含创建、修改、删除的所有审计信息和对应的用户ID
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public abstract class AuditedEntityBase<TKey, TUserKey> : AuditedEntityBase<TKey>, IFullAuditedEntity<TKey, TUserKey>
    where TKey : IEquatable<TKey>
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected AuditedEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected AuditedEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 创建者ID
    /// </summary>
    public virtual TUserKey? CreatorId { get; set; }

    /// <summary>
    /// 修改者ID
    /// </summary>
    public virtual TUserKey? ModifierId { get; set; }

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
        SoftDelete();
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

    /// <summary>
    /// 标记为已修改（记录修改者）
    /// </summary>
    /// <param name="modifierId">修改者ID</param>
    public virtual void MarkAsModified(TUserKey modifierId)
    {
        MarkAsModified();
        ModifierId = modifierId;
    }
}
