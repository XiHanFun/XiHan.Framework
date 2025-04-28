#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectConditionParser
// Guid:92cb616e-ee5f-43bd-9e99-68bc6e80959f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/29 2:53:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Paging.Dtos;
using XiHan.Framework.Utils.DataFilter.Paging.Enums;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.DataFilter.Paging.Handlers;

/// <summary>
/// 选择条件解析器
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public static class SelectConditionParser<T>
{
    /// <summary>
    /// 键选择器缓存
    /// </summary>
    private static readonly ConcurrentDictionary<string, MemberExpression> _selectConditionMemberParserCache = new();

    /// <summary>
    /// 获取选择条件解析器
    /// </summary>
    /// <param name="selectCondition"></param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> GetSelectConditionParser(SelectConditionDto selectCondition)
    {
        return GetSelectConditionParser(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
    }

    /// <summary>
    /// 获取选择条件解析器
    /// </summary>
    /// <param name="selectCondition"></param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> GetSelectConditionParser(SelectConditionDto<T> selectCondition)
    {
        return GetSelectConditionParser(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
    }

    /// <summary>
    /// 获取选择条件解析器
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <param name="value">比较值</param>
    /// <param name="selectCompare">比较操作</param>
    /// <returns>生成的 Lambda 表达式</returns>
    public static Expression<Func<T, bool>> GetSelectConditionParser(string propertyName, object? value, SelectCompareEnum selectCompare)
    {
        var type = typeof(T);
        var key = $"{typeof(T).FullName}.{propertyName}.{selectCompare}";

        var param = Expression.Parameter(type);

        if (!_selectConditionMemberParserCache.TryGetValue(key, out var propertyAccess))
        {
            var property = type.GetPropertyInfo(propertyName);
            propertyAccess = Expression.MakeMemberAccess(param, property);

            _ = _selectConditionMemberParserCache.TryAdd(key, propertyAccess);
        }

        // 生成比较表达式
        var comparison = GenerateComparison(propertyAccess, value, selectCompare);
        var expressionParser = Expression.Lambda<Func<T, bool>>(comparison, param);

        return expressionParser;
    }

    #region 私有方法

    /// <summary>
    /// 生成具体的比较表达式
    /// </summary>
    /// <param name="propertyAccess">属性访问表达式</param>
    /// <param name="value">比较值 </param>
    /// <param name="selectCompare">比较操作 </param>
    /// <returns>生成的比较表达式 </returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Expression GenerateComparison(MemberExpression propertyAccess, object? value, SelectCompareEnum selectCompare)
    {
        var constant = Expression.Constant(value);

        return selectCompare switch
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

            _ => throw new NotSupportedException($"不支持的比较操作：{selectCompare}")
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
        if (value is not Tuple<object, object> range || range.Item1 is null || range.Item2 is null)
        {
            throw new ArgumentException("Between 操作需要一个范围值（Tuple<object, object>）。");
        }

        var lowerBound = Expression.GreaterThanOrEqual(propertyAccess, Expression.Constant(range.Item1));
        var upperBound = Expression.LessThanOrEqual(propertyAccess, Expression.Constant(range.Item2));
        return Expression.AndAlso(lowerBound, upperBound);
    }

    #endregion 私有方法
}
