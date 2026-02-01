#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortConditionParser
// Guid:03f8c708-54fe-4b89-9368-97ddbbda5b7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 04:40:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Domain.Shared.Paging.Handlers;

/// <summary>
/// 排序条件解析器
/// </summary>
public static class SortConditionParser<T>
{
    /// <summary>
    /// 属性信息缓存（仅缓存属性元数据，不缓存表达式）
    /// </summary>
    private static readonly ConcurrentDictionary<string, PropertyInfo> PropertyInfoCache = new();

    /// <summary>
    /// 获取排序条件解析器
    /// </summary>
    /// <param name="condition">排序条件</param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetSortConditionParser(SortCondition condition)
    {
        return GetSortConditionParser(condition.Field);
    }

    /// <summary>
    /// 获取排序条件解析器
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static Expression<Func<T, object>> GetSortConditionParser(string field)
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

        // 将属性访问转换为 object 类型
        var converted = Expression.Convert(propertyAccess, typeof(object));
        var sortConditionParser = Expression.Lambda<Func<T, object>>(converted, param);

        return sortConditionParser;
    }
}
