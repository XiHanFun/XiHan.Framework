// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 强类型标识。
/// </summary>
/// <remarks>
/// <typeparamref name="TTag"/> 只用于区分类型，不承载数据。<c>TypedId&lt;Order, Guid&gt;</c> 与 <c>TypedId&lt;Customer, Guid&gt;</c> 不可互相赋值。线上编码为 <typeparamref name="TValue"/> 的编码（string / uuid / int64）。
/// </remarks>
/// <typeparam name="TTag">标签类型。</typeparam>
/// <typeparam name="TValue">底层值类型。</typeparam>
/// <param name="Value">底层值。</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct TypedId<TTag, TValue>(TValue Value) : ITypedId<TValue>
    where TValue : notnull, IEquatable<TValue>
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString() ?? string.Empty;
}
