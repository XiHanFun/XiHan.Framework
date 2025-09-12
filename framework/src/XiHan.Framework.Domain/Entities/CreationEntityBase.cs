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
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class CreationEntityBase<TKey> : EntityBase<TKey>, ICreationEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CreationEntityBase()
    {
        CreationTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected CreationEntityBase(TKey id) : base(id)
    {
        CreationTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTimeOffset CreationTime { get; set; }
}

/// <summary>
/// 创建审计实体基类（带创建者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
/// <typeparam name="TUserKey">用户主键类型</typeparam>
public abstract class CreationEntityBase<TKey, TUserKey> : CreationEntityBase<TKey>, ICreationEntity<TUserKey>
    where TKey : IEquatable<TKey>
    where TUserKey : IEquatable<TUserKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected CreationEntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">实体主键</param>
    protected CreationEntityBase(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 创建者ID
    /// </summary>
    public virtual TUserKey? CreatorId { get; set; }
}
