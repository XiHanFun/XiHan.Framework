#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExpressionParser
// Guid:92cb616e-ee5f-43bd-9e99-68bc6e80959f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/29 2:53:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 动态表达式解析器
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public static class ExpressionParser<T>
{
    /// <summary>
    /// 根据选择比较枚举和输入值生成 Lambda 表达式
    /// </summary>
    /// <param name="propertyName">实体属性名称</param>
    /// <param name="value">比较值</param>
    /// <param name="compare">比较操作</param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> Parse(string propertyName, object? value, SelectCompareEnum compare)
    {
        // 检查属性是否有效
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) ??
            throw new ArgumentException($"在类型 {typeof(T).Name} 中没有发现属性 {propertyName}。");

        // 创建表达式参数
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, property);

        // 生成比较表达式
        var comparison = GenerateComparison(propertyAccess, value, compare);

        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }

    #region 私有方法

    /// <summary>
    /// 生成具体的比较表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式 </param>
    /// <param name="value">比较值 </param>
    /// <param name="compare">比较操作 </param>
    /// <returns>生成的比较表达式 </returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Expression GenerateComparison(MemberExpression propertyAccess, object? value, SelectCompareEnum compare)
    {
        var constant = Expression.Constant(value);

        return compare switch
        {
            // 单值比较
            SelectCompareEnum.Equal => Expression.Equal(propertyAccess, constant),
            SelectCompareEnum.Greater => Expression.GreaterThan(propertyAccess, constant),
            SelectCompareEnum.GreaterEqual => Expression.GreaterThanOrEqual(propertyAccess, constant),
            SelectCompareEnum.Less => Expression.LessThan(propertyAccess, constant),
            SelectCompareEnum.LessEqual => Expression.LessThanOrEqual(propertyAccess, constant),
            SelectCompareEnum.NotEqual => Expression.NotEqual(propertyAccess, constant),

            // 集合比较
            SelectCompareEnum.Contains => GenerateContainsExpression(propertyAccess, value),
            SelectCompareEnum.InWithContains => GenerateInWithContainsExpression(propertyAccess, value),
            SelectCompareEnum.InWithEqual => GenerateInWithEqualExpression(propertyAccess, value),

            // 区间比较
            SelectCompareEnum.Between => GenerateBetweenExpression(propertyAccess, value),

            _ => throw new NotSupportedException($"不支持的比较操作：{compare}")
        };
    }

    /// <summary>
    /// 生成 Contains 表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">比较值</param>
    /// <returns>Contains 表达式</returns>
    /// <exception cref="ArgumentException"></exception>
    private static MethodCallExpression GenerateContainsExpression(MemberExpression propertyAccess, object? value)
    {
        if (value is not string)
        {
            throw new ArgumentException("Contains 操作仅适用于字符串类型。");
        }

        var method = typeof(string).GetMethod("Contains", [typeof(string)])!;

        // 生成 Contains 表达式
        return Expression.Call(propertyAccess, method, Expression.Constant(value));
    }

    /// <summary>
    /// 生成 InWithContains 表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">比较值</param>
    /// <returns>InWithContains 表达式</returns>
    /// <exception cref="ArgumentException"></exception>
    private static BinaryExpression GenerateInWithContainsExpression(MemberExpression propertyAccess, object? value)
    {
        if (value is not IEnumerable<string> stringList || !stringList.Any())
        {
            throw new ArgumentException("InWithContains 操作需要一个非空的字符串集合。");
        }

        var method = typeof(string).GetMethod("Contains", [typeof(string)])!;

        // 生成多个 Contains 表达式并使用 Aggregate 合并
        return stringList
            .Select(str =>
            {
                var methodCall = Expression.Call(propertyAccess, method, Expression.Constant(str));
                return Expression.Equal(methodCall, Expression.Constant(true));
            })
            .Aggregate(Expression.OrElse);
    }

    /// <summary>
    /// 生成 InWithEqual 表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">比较值</param>
    /// <returns>InWithEqual 表达式</returns>
    /// <exception cref="ArgumentException"></exception>
    private static BinaryExpression GenerateInWithEqualExpression(MemberExpression propertyAccess, object? value)
    {
        if (value is not IEnumerable<object> valueList)
        {
            throw new ArgumentException("InWithEqual 操作需要一个值集合。");
        }

        // 生成多个 Equal 表达式并连接
        return valueList
           .Select(v => Expression.Equal(propertyAccess, Expression.Constant(v)))
           .Aggregate(Expression.OrElse);
    }

    /// <summary>
    /// 生成 Between 表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">Between 操作的范围值</param>
    /// <returns>Between 表达式</returns>
    /// <exception cref="ArgumentException"></exception>
    private static BinaryExpression GenerateBetweenExpression(MemberExpression propertyAccess, object? value)
    {
        if (value is not Tuple<object, object> range || range.Item1 == null || range.Item2 == null)
        {
            throw new ArgumentException("Between 操作需要一个范围值（Tuple<object, object>）。");
        }

        var lowerBound = Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(range.Item1));
        var upperBound = Expression.LessThanOrEqual(propertyAccess, Expression.Constant(range.Item2));
        return Expression.AndAlso(lowerBound, upperBound);
    }

    #endregion
}
