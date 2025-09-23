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
    public virtual DateTimeOffset? DeletionTime { get; set; }
}

/// <summary>
/// 删除审计实体基类（带删除者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class DeletionEntityBase<TKey> : DeletionEntityBase, IDeletionEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DeletionEntityBase() : base()
    {
    }

    /// <summary>
    /// 删除者Id
    /// </summary>
    public virtual TKey? DeleterId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    public virtual string? Deleter { get; set; }
}
