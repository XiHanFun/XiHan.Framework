// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Kernel;

#pragma warning disable CA1000

/// <summary>
/// 曦寒框架可空值的可选容器。
/// </summary>
public readonly struct XiHanMaybe<T> : IEquatable<XiHanMaybe<T>>
{
    private readonly T? _value;

    private XiHanMaybe(T value)
    { _value = value; HasValue = true; }

    /// <summary>
    /// 是否有值。
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; }

    /// <summary>
    /// 获取值。仅在 <see cref="HasValue"/> 为 <c>true</c> 时安全访问。
    /// </summary>
    public T Value => HasValue ? _value! : throw new InvalidOperationException("Maybe has no value.");

    /// <summary>
    /// 创建一个有值的 Maybe。
    /// </summary>
    public static XiHanMaybe<T> Some(T value) => new(value);

    /// <summary>
    /// 无值。
    /// </summary>
    public static XiHanMaybe<T> None => default;

    /// <summary>
    /// 如果有值则转换。
    /// </summary>
    public XiHanMaybe<TOut> Map<TOut>(Func<T, TOut> mapper)
        => HasValue ? XiHanMaybe<TOut>.Some(mapper(_value!)) : XiHanMaybe<TOut>.None;

    /// <summary>
    /// 如果有值则执行可能无值的操作。
    /// </summary>
    public XiHanMaybe<TOut> Bind<TOut>(Func<T, XiHanMaybe<TOut>> binder)
        => HasValue ? binder(_value!) : XiHanMaybe<TOut>.None;

    /// <summary>
    /// 解构 Maybe，同时处理有值和无值路径。
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
        => HasValue ? onSome(_value!) : onNone();

    /// <summary>
    /// 如果有值则返回值，否则返回 <paramref name="defaultValue"/>。
    /// </summary>
    public T ValueOrDefault(T defaultValue) => HasValue ? _value! : defaultValue;

    /// <summary>
    /// 如果无值则用工厂函数创建。
    /// </summary>
    public T OrElse(Func<T> factory) => HasValue ? _value! : factory();

    /// <summary>
    /// 隐式转换：从值创建 Maybe。
    /// </summary>
    public static implicit operator XiHanMaybe<T>(T value) => Some(value);

    /// <inheritdoc />
    public bool Equals(XiHanMaybe<T> other)
        => HasValue == other.HasValue && (!HasValue || EqualityComparer<T>.Default.Equals(_value, other._value));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is XiHanMaybe<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HasValue ? _value!.GetHashCode() : 0;

    /// <inheritdoc />
    public static bool operator ==(XiHanMaybe<T> left, XiHanMaybe<T> right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(XiHanMaybe<T> left, XiHanMaybe<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => HasValue ? $"Some({_value})" : "None";
}
