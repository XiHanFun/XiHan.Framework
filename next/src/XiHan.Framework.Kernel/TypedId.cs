// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel;

/// <summary>
/// 强类型 ID，防止不同 ID 之间的错误赋值。
/// 用法：public readonly record struct OrderId(TypedId&lt;Guid&gt; Value);
/// </summary>
public readonly struct TypedId<TValue> : IEquatable<TypedId<TValue>>, IComparable<TypedId<TValue>>
    where TValue : notnull, IEquatable<TValue>, IComparable<TValue>
{
    /// <summary>
    /// ID 的值。
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// 创建一个强类型 ID。
    /// </summary>
    public TypedId(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public bool Equals(TypedId<TValue> other) => Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is TypedId<TValue> other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public int CompareTo(TypedId<TValue> other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString() ?? string.Empty;

    public static bool operator ==(TypedId<TValue> left, TypedId<TValue> right) => left.Equals(right);

    public static bool operator !=(TypedId<TValue> left, TypedId<TValue> right) => !left.Equals(right);

    public static bool operator <(TypedId<TValue> left, TypedId<TValue> right) => left.CompareTo(right) < 0;

    public static bool operator >(TypedId<TValue> left, TypedId<TValue> right) => left.CompareTo(right) > 0;

    public static bool operator <=(TypedId<TValue> left, TypedId<TValue> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(TypedId<TValue> left, TypedId<TValue> right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// 隐式转换为底层值。
    /// </summary>
    public static implicit operator TValue(TypedId<TValue> id) => id.Value;
}
