#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryFilter
// Guid:67b10bd1-1623-4f95-af56-19a45b4390c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 06:33:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 查询过滤条件
/// </summary>
public class QueryFilter
{
    private string _field = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    public QueryFilter()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="value">条件值</param>
    /// <param name="operator">比较操作符</param>
    public QueryFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        Field = field;
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="values">多值（Between / In）</param>
    /// <param name="operator">比较操作符</param>
    public QueryFilter(string field, object[]? values, QueryOperator @operator = QueryOperator.Between)
    {
        Field = field;
        Values = values;
        Operator = @operator;
    }

    /// <summary>
    /// 字段名称
    /// </summary>
    public string Field
    {
        get => _field;
        set => _field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("字段名称不能为空", nameof(Field))
            : value.Trim();
    }

    /// <summary>
    /// 比较值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 多值（Between / In）
    /// </summary>
    public object[]? Values { get; set; }

    /// <summary>
    /// 比较操作符，默认为等于
    /// </summary>
    public QueryOperator Operator { get; set; } = QueryOperator.Equal;

    /// <summary>
    /// 创建等于条件
    /// </summary>
    public static QueryFilter Equal(string field, object value) => new(field, value, QueryOperator.Equal);

    /// <summary>
    /// 创建不等于条件
    /// </summary>
    public static QueryFilter NotEqual(string field, object value) => new(field, value, QueryOperator.NotEqual);

    /// <summary>
    /// 创建大于条件
    /// </summary>
    public static QueryFilter GreaterThan(string field, object value) => new(field, value, QueryOperator.GreaterThan);

    /// <summary>
    /// 创建大于等于条件
    /// </summary>
    public static QueryFilter GreaterThanOrEqual(string field, object value) => new(field, value, QueryOperator.GreaterThanOrEqual);

    /// <summary>
    /// 创建小于条件
    /// </summary>
    public static QueryFilter LessThan(string field, object value) => new(field, value, QueryOperator.LessThan);

    /// <summary>
    /// 创建小于等于条件
    /// </summary>
    public static QueryFilter LessThanOrEqual(string field, object value) => new(field, value, QueryOperator.LessThanOrEqual);

    /// <summary>
    /// 创建包含条件
    /// </summary>
    public static QueryFilter Contains(string field, string value) => new(field, value, QueryOperator.Contains);

    /// <summary>
    /// 创建以...开始条件
    /// </summary>
    public static QueryFilter StartsWith(string field, string value) => new(field, value, QueryOperator.StartsWith);

    /// <summary>
    /// 创建以...结束条件
    /// </summary>
    public static QueryFilter EndsWith(string field, string value) => new(field, value, QueryOperator.EndsWith);

    /// <summary>
    /// 创建在集合中条件
    /// </summary>
    public static QueryFilter In(string field, params object[] values) => new(field, values, QueryOperator.In);

    /// <summary>
    /// 创建不在集合中条件
    /// </summary>
    public static QueryFilter NotIn(string field, params object[] values) => new(field, values, QueryOperator.NotIn);

    /// <summary>
    /// 创建区间条件
    /// </summary>
    public static QueryFilter Between(string field, object start, object end) => new(field, new[] { start, end }, QueryOperator.Between);

    /// <summary>
    /// 创建为空条件
    /// </summary>
    public static QueryFilter IsNull(string field) => new(field, null, QueryOperator.IsNull);

    /// <summary>
    /// 创建不为空条件
    /// </summary>
    public static QueryFilter IsNotNull(string field) => new(field, null, QueryOperator.IsNotNull);

    /// <summary>
    /// 验证过滤条件是否有效
    /// </summary>
    public bool IsValid()
    {
        // 字段名不能为空
        if (string.IsNullOrWhiteSpace(Field))
        {
            return false;
        }

        // IsNull 和 IsNotNull 操作符不需要值
        if (Operator is QueryOperator.IsNull or QueryOperator.IsNotNull)
        {
            return true;
        }

        // In / NotIn / Between 使用 Values
        if (Operator is QueryOperator.In or QueryOperator.NotIn)
        {
            return Values is { Length: > 0 };
        }

        if (Operator is QueryOperator.Between)
        {
            return Values is { Length: 2 };
        }

        // 其他操作符使用 Value
        return Value is not null;
    }
}
