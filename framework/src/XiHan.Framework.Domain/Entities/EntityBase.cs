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
using XiHan.Framework.Domain.Entities.Abstracts;

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
public abstract class EntityBase<TKey> : EntityBase, IEntityBase<TKey>, IEquatable<EntityBase<TKey>>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected EntityBase() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">实体主键</param>
    protected EntityBase(TKey basicId) : base()
    {
        BasicId = basicId;
    }

    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 相等运算符重载
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public static bool operator ==(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// 不等运算符重载
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>如果不等返回 true，否则返回 false</returns>
    public static bool operator !=(EntityBase<TKey>? left, EntityBase<TKey>? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 实体相等性比较
    /// </summary>
    /// <param name="other">另一个实体</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public virtual bool Equals(EntityBase<TKey>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        // 如果任一实体的 Id 为默认值，则认为不相等（临时实体）
        if (EqualityComparer<TKey>.Default.Equals(BasicId, default!) ||
            EqualityComparer<TKey>.Default.Equals(other.BasicId, default!))
        {
            return false;
        }

        return BasicId.Equals(other.BasicId);
    }

    /// <summary>
    /// 重写 Equals 方法
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as EntityBase<TKey>);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// 优化了临时实体的哈希码计算
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        // 如果 Id 为默认值，使用基类的 GetHashCode
        if (IsTransient())
        {
            return base.GetHashCode();
        }

        // 使用 HashCode.Combine 提供更好的哈希分布
        return HashCode.Combine(GetType(), BasicId);
    }

    /// <summary>
    /// 检查实体是否为临时实体（尚未持久化）
    /// </summary>
    /// <returns>如果是临时实体返回 true，否则返回 false</returns>
    public virtual bool IsTransient()
    {
        return EqualityComparer<TKey>.Default.Equals(BasicId, default!);
    }
}
