// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// <see cref="Maybe{T}"/> 的工厂方法。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class Maybe
{
    /// <summary>
    /// 创建有值的 <see cref="Maybe{T}"/>。
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>。
    /// </remarks>
    public static Maybe<T> Some<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value);
    }

    /// <summary>
    /// 创建无值的 <see cref="Maybe{T}"/>。
    /// </summary>
    public static Maybe<T> None<T>() => default;

    /// <summary>
    /// 从可空引用创建：<see langword="null"/> 为无值。
    /// </summary>
    public static Maybe<T> From<T>(T? value)
        where T : class
        => value is null ? default : new(value);

    /// <summary>
    /// 从可空值类型创建：<see langword="null"/> 为无值。
    /// </summary>
    public static Maybe<T> From<T>(T? value)
        where T : struct
        => value.HasValue ? new(value.Value) : default;
}
