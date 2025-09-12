#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EntityBase
// Guid:01fa143e-06f6-4ce4-a29b-bd2ee3a440b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 2:51:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 实体基类
/// </summary>
public abstract class EntityBase : IEntityBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected EntityBase()
    {
        RowVersion = [];
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [ConcurrencyCheck]
    [Timestamp]
    public virtual byte[] RowVersion { get; set; }
}

/// <summary>
/// 泛型主键实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class EntityBase<TKey> : EntityBase, IEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected EntityBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id"></param>
    protected EntityBase(TKey id)
    {
        BasicId = id;
    }

    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; protected set; } = default!;
}
