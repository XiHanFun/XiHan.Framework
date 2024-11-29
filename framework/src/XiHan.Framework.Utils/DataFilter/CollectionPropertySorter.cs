#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CollectionPropertySorter
// Guid:1038670b-e9f2-45f0-82b5-eb4ce622a25d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 3:04:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 集合属性排序器
/// </summary>
public static class CollectionPropertySorter<T>
{
    /// <summary>
    /// 对 IEnumerable 集合根据属性名称进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, string propertyName, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        var keySelector = KeySelector<T>.GetKeySelector(propertyName);
        return OrderBy(source, selector => keySelector, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 集合进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        return sortDirection == SortDirectionEnum.Asc
            ? source.OrderBy(keySelector.Compile())
            : source.OrderByDescending(keySelector.Compile());
    }

    /// <summary>
    /// 对 IQueryable 集合根据属性名称进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, string propertyName, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        var keySelector = KeySelector<T>.GetKeySelectorExpression(propertyName);
        return OrderBy(source, selector => keySelector, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 集合进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        return sortDirection == SortDirectionEnum.Asc
            ? source.OrderBy(keySelector)
            : source.OrderByDescending(keySelector);
    }

    /// <summary>
    /// 对 IEnumerable 已排序集合根据属性名称进行后续排序
    /// </summary>
    /// <param name="orderedSource">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy(IOrderedEnumerable<T> orderedSource, string propertyName, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        var keySelector = KeySelector<T>.GetKeySelector(propertyName);
        return ThenBy(orderedSource, selector => keySelector, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 已排序集合进行后续排序
    /// </summary>
    /// <param name="orderedSource">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy(IOrderedEnumerable<T> orderedSource, Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        return sortDirection == SortDirectionEnum.Asc
            ? orderedSource.ThenBy(keySelector.Compile())
            : orderedSource.ThenByDescending(keySelector.Compile());
    }

    /// <summary>
    /// 对 IQueryable 已排序集合根据属性名称进行后续排序
    /// </summary>
    /// <param name="orderedSource">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> orderedSource, string propertyName, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        var keySelector = KeySelector<T>.GetKeySelectorExpression(propertyName);
        return ThenBy(orderedSource, selector => keySelector, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 已排序集合进行后续排序
    /// </summary>
    /// <param name="orderedSource">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> orderedSource, Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        return sortDirection == SortDirectionEnum.Asc
            ? orderedSource.ThenBy(keySelector)
            : orderedSource.ThenByDescending(keySelector);
    }
}
