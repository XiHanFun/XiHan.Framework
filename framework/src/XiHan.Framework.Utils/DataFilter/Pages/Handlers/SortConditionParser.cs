#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortConditionParser
// Guid:03f8c708-54fe-4b89-9368-97ddbbda5b7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:40:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Pages.Dtos;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.DataFilter.Pages.Handlers;

/// <summary>
/// 排序条件解析器
/// </summary>
public static class SortConditionParser<T>
{
    /// <summary>
    /// 排序条件缓存
    /// </summary>
    private static readonly ConcurrentDictionary<string, LambdaExpression> SortConditionParserCache = new();

    /// <summary>
    /// 获取排序条件解析器
    /// </summary>
    /// <param name="sortCondition"></param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetSortConditionParser(SortConditionDto sortCondition)
    {
        return GetSortConditionParser(sortCondition.SortField);
    }

    /// <summary>
    /// 获取排序条件解析器
    /// </summary>
    /// <param name="sortCondition"></param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetSortConditionParser(SortConditionDto<T> sortCondition)
    {
        return GetSortConditionParser(sortCondition.SortField);
    }

    /// <summary>
    /// 获取排序条件解析器
    /// </summary>
    /// <param name="propertyName">属性名称</param>s
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static Expression<Func<T, object>> GetSortConditionParser(string propertyName)
    {
        var type = typeof(T);
        var key = $"{type.FullName}.{propertyName}";
        if (SortConditionParserCache.TryGetValue(key, out var sortConditionParser))
        {
            return (Expression<Func<T, object>>)sortConditionParser;
        }

        var param = Expression.Parameter(type);
        var property = type.GetPropertyInfo(propertyName);
        var propertyAccess = Expression.MakeMemberAccess(param, property);

        // 将属性访问转换为 object 类型
        var converted = Expression.Convert(propertyAccess, typeof(object));
        sortConditionParser = Expression.Lambda<Func<T, object>>(converted, param);

        _ = SortConditionParserCache.TryAdd(key, sortConditionParser);

        return (Expression<Func<T, object>>)sortConditionParser;
    }
}
