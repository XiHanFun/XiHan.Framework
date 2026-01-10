#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectCondition
// Guid:67b10bd1-1623-4f95-af56-19a45b4390c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:33:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 选择条件（过滤条件）
/// </summary>
public class SelectCondition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SelectCondition()
    {
        Field = string.Empty;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="value">条件值</param>
    /// <param name="operator">比较操作符</param>
    public SelectCondition(string field, object? value, SelectCompare @operator = SelectCompare.Equal)
    {
        Field = field;
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// 字段名称
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// 条件值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 比较操作符，默认为等于
    /// </summary>
    public SelectCompare Operator { get; set; } = SelectCompare.Equal;

    /// <summary>
    /// 创建等于条件
    /// </summary>
    public static SelectCondition Equal(string field, object? value) =>
        new(field, value, SelectCompare.Equal);

    /// <summary>
    /// 创建不等于条件
    /// </summary>
    public static SelectCondition NotEqual(string field, object? value) =>
        new(field, value, SelectCompare.NotEqual);

    /// <summary>
    /// 创建大于条件
    /// </summary>
    public static SelectCondition GreaterThan(string field, object? value) =>
        new(field, value, SelectCompare.Greater);

    /// <summary>
    /// 创建大于等于条件
    /// </summary>
    public static SelectCondition GreaterThanOrEqual(string field, object? value) =>
        new(field, value, SelectCompare.GreaterEqual);

    /// <summary>
    /// 创建小于条件
    /// </summary>
    public static SelectCondition LessThan(string field, object? value) =>
        new(field, value, SelectCompare.Less);

    /// <summary>
    /// 创建小于等于条件
    /// </summary>
    public static SelectCondition LessThanOrEqual(string field, object? value) =>
        new(field, value, SelectCompare.LessEqual);

    /// <summary>
    /// 创建包含条件（字符串）
    /// </summary>
    public static SelectCondition Contains(string field, string value) =>
        new(field, value, SelectCompare.Contains);

    /// <summary>
    /// 创建在集合中条件
    /// </summary>
    public static SelectCondition In(string field, IEnumerable<object> values) =>
        new(field, values, SelectCompare.InWithEqual);

    /// <summary>
    /// 创建在区间内条件
    /// </summary>
    public static SelectCondition Between(string field, object minValue, object maxValue) =>
        new(field, new Tuple<object, object>(minValue, maxValue), SelectCompare.Between);
}

/// <summary>
/// 泛型选择条件（支持 Lambda 表达式）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class SelectCondition<T> : SelectCondition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SelectCondition() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="value">条件值</param>
    /// <param name="operator">比较操作符</param>
    public SelectCondition(string field, object? value, SelectCompare @operator = SelectCompare.Equal)
        : base(field, value, @operator)
    {
    }

    /// <summary>
    /// 构造函数（使用 Lambda 表达式）
    /// </summary>
    /// <param name="selector">属性选择器</param>
    /// <param name="value">条件值</param>
    /// <param name="operator">比较操作符</param>
    public SelectCondition(Expression<Func<T, object>> selector, object? value, SelectCompare @operator = SelectCompare.Equal)
    {
        Field = GetPropertyName(selector);
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// 创建等于条件
    /// </summary>
    public static SelectCondition<T> Equal(Expression<Func<T, object>> selector, object? value) =>
        new(selector, value, SelectCompare.Equal);

    /// <summary>
    /// 创建包含条件
    /// </summary>
    public static SelectCondition<T> Contains(Expression<Func<T, object>> selector, string value) =>
        new(selector, value, SelectCompare.Contains);

    /// <summary>
    /// 获取属性名称
    /// </summary>
    /// <param name="selector">属性选择器</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static string GetPropertyName(Expression<Func<T, object>> selector)
    {
        if (selector.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (selector.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("Invalid property selector", nameof(selector));
    }
}
