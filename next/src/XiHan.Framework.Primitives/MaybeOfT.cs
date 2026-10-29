// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 类型层面的"可能没有值"。
/// </summary>
/// <remarks>
/// <c>default</c> 即无值。用 <see cref="Maybe.Some{T}(T)"/> 与 <see cref="Maybe.None{T}()"/> 创建。线上编码：有值为值本身，无值为 <c>null</c>。
/// </remarks>
/// <typeparam name="T">值类型。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct Maybe<T>
{
    /// <summary>
    /// 是否有值。
    /// </summary>
    /// <remarks>
    /// <c>default</c> 实例为 <see langword="false"/>。
    /// </remarks>
    private readonly bool _hasValue;

    /// <summary>
    /// 有值时的值。
    /// </summary>
    /// <remarks>
    /// 无值时为类型默认值，不得读取。
    /// </remarks>
    private readonly T? _value;

    /// <summary>
    /// 创建有值实例。
    /// </summary>
    /// <remarks>
    /// 只供 <see cref="Maybe"/> 的工厂方法调用，调用方已保证值非空。
    /// </remarks>
    /// <param name="value">非空的值。</param>
    internal Maybe(T value)
    {
        _hasValue = true;
        _value = value;
    }

    /// <summary>
    /// 是否有值。
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// 把值隐式转换为有值实例。
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>。
    /// </remarks>
    public static implicit operator Maybe<T>(T value) => Maybe.Some(value);

    /// <summary>
    /// 有值时取值。
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _hasValue;
    }

    /// <summary>
    /// 有值时返回值，否则返回 <paramref name="fallback"/>。
    /// </summary>
    public T ValueOr(T fallback) => _hasValue ? _value! : fallback;

    /// <summary>
    /// 有值时返回值，否则抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public T ValueOrThrow() => _hasValue ? _value! : throw new InvalidOperationException("The Maybe has no value.");

    /// <summary>
    /// 按有值或无值分支求值。
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone)
    {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);
        return _hasValue ? onSome(_value!) : onNone();
    }

    /// <summary>
    /// 有值时映射，无值时保持无值。
    /// </summary>
    public Maybe<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return _hasValue ? Maybe.Some(map(_value!)) : default;
    }

    /// <summary>
    /// 有值时继续执行 <paramref name="bind"/>，无值时保持无值。
    /// </summary>
    public Maybe<TOut> Bind<TOut>(Func<T, Maybe<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _hasValue ? bind(_value!) : default;
    }

    /// <summary>
    /// 有值时转换为成功结果，无值时转换为携带 <paramref name="error"/> 的失败结果。
    /// </summary>
    public Result<T> ToResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return _hasValue ? Result.Success(_value!) : Result.Failure<T>(error);
    }

    /// <inheritdoc />
    public override string ToString() => _hasValue ? $"Some({_value})" : "None";
}
