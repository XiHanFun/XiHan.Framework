#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CreationEntityBase
// Guid:d3c3c3ee-6719-4e6b-8a92-c54dda3937c3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 3:01:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
/// 创建审计实体基类（带创建者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class CreationEntityBase<TKey> : CreationEntityBase, ICreationEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CreationEntityBase() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="createdId"></param>
    protected CreationEntityBase(TKey createdId) : this()
    {
        CreatedId = createdId;
    }

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    public virtual TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string? CreatedBy { get; set; }
}
