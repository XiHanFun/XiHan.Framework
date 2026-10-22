// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 编译后的子句
/// </summary>
/// <remarks>
/// 编译出错的子句（<see cref="Error"/> 非空）照样保留，求值时恒为「无法判定」，这样存量脏数据不会让整个条件集失效。
/// </remarks>
public sealed class CompiledConditionClause
{
    internal CompiledConditionClause(
        ConditionClause source,
        ConditionAttributeResolution? resolution,
        string? error,
        object? scalar = null,
        IReadOnlyList<object>? items = null,
        (object Min, object Max)? range = null)
    {
        Source = source;
        Resolution = resolution;
        Error = error;
        Scalar = scalar;
        Items = items;
        Range = range;
    }

    /// <summary>
    /// 原始子句
    /// </summary>
    public ConditionClause Source { get; }

    /// <summary>
    /// 属性解析结果；属性没登记时为空
    /// </summary>
    public ConditionAttributeResolution? Resolution { get; }

    /// <summary>
    /// 编译错误；为空表示编译成功
    /// </summary>
    public string? Error { get; }

    internal object? Scalar { get; }

    internal IReadOnlyList<object>? Items { get; }

    internal (object Min, object Max)? Range { get; }
}
