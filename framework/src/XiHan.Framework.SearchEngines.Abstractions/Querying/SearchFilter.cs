// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.SearchEngines.Querying;

/// <summary>
/// 结构化过滤运算符
/// </summary>
/// <remarks>
/// 每一项都能同时翻译为 Elasticsearch 查询与 SQL 谓词，不收录任一后端独有的运算符。
/// </remarks>
public enum SearchFilterOperator
{
    /// <summary>
    /// 等于
    /// </summary>
    Equal,

    /// <summary>
    /// 不等于
    /// </summary>
    NotEqual,

    /// <summary>
    /// 属于给定集合
    /// </summary>
    In,

    /// <summary>
    /// 大于
    /// </summary>
    GreaterThan,

    /// <summary>
    /// 大于等于
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// 小于
    /// </summary>
    LessThan,

    /// <summary>
    /// 小于等于
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// 字段存在且非空
    /// </summary>
    Exists,

    /// <summary>
    /// 前缀匹配
    /// </summary>
    StartsWith
}

/// <summary>
/// 结构化过滤条件
/// </summary>
/// <remarks>
/// 过滤条件之间恒为「与」关系。需要「或」时用 <see cref="SearchFilterOperator.In"/> 表达同字段多值，
/// 跨字段的「或」不在本抽象范围内——它在两类后端上的语义与性能差异过大，交由调用方拆分查询。
/// </remarks>
public sealed class SearchFilter
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名</param>
    /// <param name="op">运算符</param>
    /// <param name="value">单值，<see cref="SearchFilterOperator.In"/> 与 <see cref="SearchFilterOperator.Exists"/> 时忽略</param>
    /// <param name="values">多值，仅 <see cref="SearchFilterOperator.In"/> 使用</param>
    public SearchFilter(string field, SearchFilterOperator op, object? value = null, IReadOnlyList<object>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (op == SearchFilterOperator.In && (values is null || values.Count == 0))
        {
            throw new ArgumentException("In 运算符必须提供非空的候选值集合。", nameof(values));
        }

        if (op is not (SearchFilterOperator.In or SearchFilterOperator.Exists) && value is null)
        {
            throw new ArgumentException($"{op} 运算符必须提供比较值。", nameof(value));
        }

        Field = field;
        Operator = op;
        Value = value;
        Values = values ?? [];
    }

    /// <summary>
    /// 字段名
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// 运算符
    /// </summary>
    public SearchFilterOperator Operator { get; }

    /// <summary>
    /// 比较值
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// 候选值集合
    /// </summary>
    public IReadOnlyList<object> Values { get; }
}
