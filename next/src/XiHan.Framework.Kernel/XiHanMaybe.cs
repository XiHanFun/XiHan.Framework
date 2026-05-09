// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Kernel;

public readonly struct XiHanMaybe<T> : IEquatable<XiHanMaybe<T>>
{
    private readonly T? _value;

    private XiHanMaybe(T value)
    { _value = value; HasValue = true; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; }

    public T Value => HasValue ? _value! : throw new InvalidOperationException("Maybe has no value.");

    public static XiHanMaybe<T> Some(T value) => new(value);

    public static XiHanMaybe<T> None => default;

    public XiHanMaybe<TOut> Map<TOut>(Func<T, TOut> mapper)
        => HasValue ? XiHanMaybe<TOut>.Some(mapper(_value!)) : XiHanMaybe<TOut>.None;

    public XiHanMaybe<TOut> Bind<TOut>(Func<T, XiHanMaybe<TOut>> binder)
        => HasValue ? binder(_value!) : XiHanMaybe<TOut>.None;

    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
        => HasValue ? onSome(_value!) : onNone();

    public T ValueOrDefault(T defaultValue) => HasValue ? _value! : defaultValue;

    public T OrElse(Func<T> factory) => HasValue ? _value! : factory();

    public static implicit operator XiHanMaybe<T>(T value) => Some(value);

    public bool Equals(XiHanMaybe<T> other)
        => HasValue == other.HasValue && (!HasValue || EqualityComparer<T>.Default.Equals(_value, other._value));

    public override bool Equals(object? obj) => obj is XiHanMaybe<T> other && Equals(other);

    public override int GetHashCode() => HasValue ? _value!.GetHashCode() : 0;

    public static bool operator ==(XiHanMaybe<T> left, XiHanMaybe<T> right) => left.Equals(right);

    public static bool operator !=(XiHanMaybe<T> left, XiHanMaybe<T> right) => !left.Equals(right);

    public override string ToString() => HasValue ? $"Some({_value})" : "None";
}
