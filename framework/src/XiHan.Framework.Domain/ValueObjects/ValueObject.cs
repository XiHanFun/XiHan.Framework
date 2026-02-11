#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ValueObject
// Guid:bce12f9d-8e3a-4b5c-9a2e-1234567890ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 15:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.ValueObjects;

/// <summary>
/// 值对象基类
/// 值对象是不可变的，通过属性值而非引用来标识相等性
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// 相等运算符重载
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right)
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
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 相等性比较
    /// 优化了性能，支持延迟求值和早期退出
    /// </summary>
    /// <param name="other">另一个值对象</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public virtual bool Equals(ValueObject? other)
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

        // 使用延迟求值和早期退出来提高性能
        using var thisEnumerator = GetEqualityComponents().GetEnumerator();
        using var otherEnumerator = other.GetEqualityComponents().GetEnumerator();

        while (true)
        {
            var thisHasNext = thisEnumerator.MoveNext();
            var otherHasNext = otherEnumerator.MoveNext();

            // 如果两者都没有更多元素，则相等
            if (!thisHasNext && !otherHasNext)
            {
                return true;
            }

            // 如果只有一个有更多元素，则不相等
            if (thisHasNext != otherHasNext)
            {
                return false;
            }

            // 比较当前元素
            if (!Equals(thisEnumerator.Current, otherEnumerator.Current))
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 重写 Equals 方法
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>如果相等返回 true，否则返回 false</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ValueObject);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// 使用 HashCode.Combine 提供更好的哈希分布和性能
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hashCode.Add(component);
        }
        return hashCode.ToHashCode();
    }

    /// <summary>
    /// 重写 ToString 方法
    /// 使用 StringBuilder 提高性能，支持更好的格式化
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        var typeName = GetType().Name;
        var components = GetEqualityComponents().ToList();

        if (components.Count == 0)
        {
            return typeName;
        }

        var sb = new System.Text.StringBuilder(typeName);
        sb.Append(" { ");

        for (var i = 0; i < components.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append($"Property{i}: {components[i] ?? "null"}");
        }

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// 获取相等性比较的属性值
    /// 子类必须实现此方法，返回用于相等性比较的所有属性值
    /// </summary>
    /// <returns>用于相等性比较的属性值集合</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}
