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

using XiHan.Framework.Ddd.Domain.Shared.Entities;

namespace XiHan.Framework.Ddd.Domain.Entities;

/// <summary>
/// 实体基类
/// </summary>
public abstract class EntityBase : IEntityBase
{
    /// <summary>
    /// 重写实体相等性判断
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        // 默认情况下仅使用引用比较，具体实体可以重写此方法
        return false;
    }

    /// <summary>
    /// 重写哈希码生成逻辑
    /// </summary>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}

/// <summary>
/// 泛型主键实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class EntityBase<TKey> : EntityBase, IEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 主键
    /// </summary>
    public virtual TKey BasicId { get; protected set; } = default!;

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
    /// 重写 Equals 方法，基于主键比较两个实体
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        if (obj is not EntityBase<TKey> other)
        {
            return false;
        }

        // 相同引用或者两个主键均不为空且相等，则认为两个实体相等
        return ReferenceEquals(this, obj) || (BasicId is not null && other.BasicId is not null && BasicId.Equals(other.BasicId));
    }

    /// <summary>
    /// 根据主键生成哈希码
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return BasicId is null ? 0 : BasicId.GetHashCode();
    }

    /// <summary>
    /// 重载 ToString 方法
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"{GetType().Name}({BasicId})";
    }

    /// <summary>
    /// 重载 == 运算符
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator ==(EntityBase<TKey> a, EntityBase<TKey> b)
    {
        return ReferenceEquals(a, b) || (a is not null && b is not null && a.Equals(b));
    }

    /// <summary>
    /// 重载 != 运算符
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator !=(EntityBase<TKey> a, EntityBase<TKey> b)
    {
        return !(a == b);
    }
}
