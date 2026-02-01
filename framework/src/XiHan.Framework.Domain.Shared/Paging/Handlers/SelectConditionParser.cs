#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectConditionParser
// Guid:92cb616e-ee5f-43bd-9e99-68bc6e80959f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/29 02:53:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Domain.Shared.Paging.Handlers;

/// <summary>
/// 选择条件解析器
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public static class SelectConditionParser<T>
{
    /// <summary>
    /// 属性信息缓存（仅缓存属性元数据，不缓存表达式）
    /// </summary>
    private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyInfoCache = new();

    /// <summary>
    /// 获取选择条件解析器
    /// </summary>
    /// <param name="condition">选择条件</param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> GetSelectConditionParser(SelectCondition condition)
    {
        return GetSelectConditionParser(condition.Field, condition.Value, condition.Operator);
    }

    /// <summary>
    /// 获取选择条件解析器
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="value">比较值</param>
    /// <param name="operator">比较操作符</param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> GetSelectConditionParser(string field, object? value, SelectCompare @operator)
    {
        var type = typeof(T);
        var param = Expression.Parameter(type, "x");

        // 从缓存获取或创建属性信息
        var key = $"{type.FullName}.{field}";
        if (!PropertyInfoCache.TryGetValue(key, out var property))
        {
            property = type.GetPropertyInfo(field);
            PropertyInfoCache.TryAdd(key, property);
        }

        // 每次创建新的属性访问表达式（必须使用当前 Parameter）
        var propertyAccess = Expression.MakeMemberAccess(param, property);

        // 生成比较表达式
        var comparison = GenerateComparison(propertyAccess, value, @operator);
        var expressionParser = Expression.Lambda<Func<T, bool>>(comparison, param);

        return expressionParser;
    }

    #region 私有方法

    /// <summary>
    /// 生成具体的比较表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">比较值</param>
    /// <param name="operator">比较操作符</param>
    /// <returns>生成的比较表达式</returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Expression GenerateComparison(MemberExpression propertyAccess, object? value, SelectCompare @operator)
    {
        return @operator switch
        {
            // 单值比较
            SelectCompare.Equal => GenerateEqualExpression(propertyAccess, value),
            SelectCompare.Greater => GenerateGreaterExpression(propertyAccess, value),
            SelectCompare.GreaterEqual => GenerateGreaterEqualExpression(propertyAccess, value),
            SelectCompare.Less => GenerateLessExpression(propertyAccess, value),
            SelectCompare.LessEqual => GenerateLessEqualExpression(propertyAccess, value),
            SelectCompare.NotEqual => GenerateNotEqualExpression(propertyAccess, value),

            // 集合比较
            SelectCompare.Contains => GenerateContainsExpression(propertyAccess, value),
            SelectCompare.InWithContains => GenerateInWithContainsExpression(propertyAccess, value),
            SelectCompare.InWithEqual => GenerateInWithEqualExpression(propertyAccess, value),

            // 区间比较
            SelectCompare.Between => GenerateBetweenExpression(propertyAccess, value),

            _ => throw new NotSupportedException($"不支持的比较操作：{@operator}")
        };
    }

    /// <summary>
    /// 生成 Equal 表达式
    /// </summary>
    private static BinaryExpression GenerateEqualExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.Equal(propertyAccess, constant);
    }

    /// <summary>
    /// 生成 NotEqual 表达式
    /// </summary>
    private static BinaryExpression GenerateNotEqualExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.NotEqual(propertyAccess, constant);
    }

    /// <summary>
    /// 生成 Greater 表达式
    /// </summary>
    private static BinaryExpression GenerateGreaterExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.GreaterThan(propertyAccess, constant);
    }

    /// <summary>
    /// 生成 GreaterEqual 表达式
    /// </summary>
    private static BinaryExpression GenerateGreaterEqualExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.GreaterThanOrEqual(propertyAccess, constant);
    }

    /// <summary>
    /// 生成 Less 表达式
    /// </summary>
    private static BinaryExpression GenerateLessExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.LessThan(propertyAccess, constant);
    }

    /// <summary>
    /// 生成 LessEqual 表达式
    /// </summary>
    private static BinaryExpression GenerateLessEqualExpression(MemberExpression propertyAccess, object? value)
    {
        var constant = Expression.Constant(value, propertyAccess.Type);
        return Expression.LessThanOrEqual(propertyAccess, constant);
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

        var propertyType = propertyAccess.Type;

        // 生成多个 Equal 表达式并连接
        return valueList
            .Select(v => Expression.Equal(propertyAccess, Expression.Constant(v, propertyType)))
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
        if (value is not Tuple<object, object> range || range.Item1 is null || range.Item2 is null)
        {
            throw new ArgumentException("Between 操作需要一个范围值(Tuple<object, object>)。");
        }

        var propertyType = propertyAccess.Type;
        var lowerBound = Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(range.Item1, propertyType));
        var upperBound = Expression.LessThanOrEqual(propertyAccess, Expression.Constant(range.Item2, propertyType));
        return Expression.AndAlso(lowerBound, upperBound);
    }

    #endregion 私有方法
}
