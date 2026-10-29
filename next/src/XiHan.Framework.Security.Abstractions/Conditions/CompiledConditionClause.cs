// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 编译后的子句。
/// </summary>
/// <remarks>
/// 编译出错的子句（<see cref="Error"/> 非空）照样保留，求值时恒为无法判定，这样存量脏数据不会让整个条件集失效。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CompiledConditionClause
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 条件值按运算符解析成标量、集合或区间之一。
    /// </remarks>
    internal CompiledConditionClause(ConditionClause source, ConditionAttributeResolution? resolution, Error? error, object? scalar = null, IReadOnlyList<object>? items = null, (object Min, object Max)? range = null)
    {
        Source = source;
        Resolution = resolution;
        Error = error;
        Scalar = scalar;
        Items = items;
        Range = range;
    }

    /// <summary>
    /// 原始子句。
    /// </summary>
    public ConditionClause Source { get; }

    /// <summary>
    /// 属性解析结果。
    /// </summary>
    /// <remarks>
    /// 属性没登记时为 <see langword="null"/>。
    /// </remarks>
    public ConditionAttributeResolution? Resolution { get; }

    /// <summary>
    /// 编译错误（错误码 <see cref="SecurityErrors.ConditionInvalid"/>）。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示编译成功。
    /// </remarks>
    public Error? Error { get; }

    /// <summary>
    /// 标量条件值（等于、比大小、包含、开头、结尾）。
    /// </summary>
    internal object? Scalar { get; }

    /// <summary>
    /// 集合条件值（在集合中、不在集合中）。
    /// </summary>
    /// <remarks>
    /// IP 集合的元素可以是网段。
    /// </remarks>
    internal IReadOnlyList<object>? Items { get; }

    /// <summary>
    /// 区间条件值（在范围内）。
    /// </summary>
    internal (object Min, object Max)? Range { get; }
}
