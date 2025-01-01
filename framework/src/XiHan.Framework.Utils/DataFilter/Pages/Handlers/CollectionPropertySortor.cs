#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CollectionPropertySortor
// Guid:1038670b-e9f2-45f0-82b5-eb4ce622a25d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 3:04:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Pages.Dtos;
using XiHan.Framework.Utils.DataFilter.Pages.Enums;

namespace XiHan.Framework.Utils.DataFilter.Pages.Handlers;

/// <summary>
/// 集合属性排序器
/// </summary>
public static class CollectionPropertySortor<T>
{
    #region IEnumerable

    /// <summary>
    /// 对 IEnumerable 集合根据键名称进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, string keyName, SortDirectionEnum sortDirection)
    {
        var keySelectorExpression = SortConditionParser<T>.GetSortConditionParser(keyName);
        var keySelector = keySelectorExpression;

        return sortDirection == SortDirectionEnum.Asc
            ? source.OrderBy(keySelector.Compile())
            : source.OrderByDescending(keySelector.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据排序条件进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, SortConditionDto sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 集合根据排序条件进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, SortConditionDto<T> sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 已排序集合根据键名称进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy(IOrderedEnumerable<T> source, string keyName, SortDirectionEnum sortDirection)
    {
        var keySelectorExpression = SortConditionParser<T>.GetSortConditionParser(keyName);
        var keySelector = keySelectorExpression;

        return sortDirection == SortDirectionEnum.Asc
            ? source.ThenBy(keySelector.Compile())
            : source.ThenByDescending(keySelector.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 已排序集合根据排序条件进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy(IOrderedEnumerable<T> source, SortConditionDto sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 已排序集合根据排序条件进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy(IOrderedEnumerable<T> source, SortConditionDto<T> sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IEnumerable 集合根据排序条件进行多条件排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortConditions">多排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, IEnumerable<SortConditionDto> sortConditions)
    {
        // 按优先级升序排列排序条件
        sortConditions = sortConditions.OrderBy(x => x.SortPriority);

        // 按优先级依次应用排序
        var firstCondition = sortConditions.First();
        var orderedSource = OrderBy(source, firstCondition);

        foreach (var sortCondition in sortConditions.Skip(1))
        {
            orderedSource = ThenBy(orderedSource, sortCondition);
        }

        return orderedSource;
    }

    /// <summary>
    /// 对 IEnumerable 集合根据排序条件进行多条件排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortConditions">多排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy(IEnumerable<T> source, IEnumerable<SortConditionDto<T>> sortConditions)
    {
        // 按优先级升序排列排序条件
        sortConditions = sortConditions.OrderBy(x => x.SortPriority);

        // 按优先级依次应用排序
        var firstCondition = sortConditions.First();
        var orderedSource = OrderBy(source, firstCondition);

        foreach (var sortCondition in sortConditions.Skip(1))
        {
            orderedSource = ThenBy(orderedSource, sortCondition);
        }

        return orderedSource;
    }

    #endregion IEnumerable

    #region IQueryable

    /// <summary>
    /// 对 IQueryable 集合根据键名称进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, string keyName, SortDirectionEnum sortDirection)
    {
        var keySelectorExpression = SortConditionParser<T>.GetSortConditionParser(keyName);
        var keySelector = keySelectorExpression;

        return sortDirection == SortDirectionEnum.Asc
            ? source.OrderBy(keySelector)
            : source.OrderByDescending(keySelector);
    }

    /// <summary>
    /// 对 IQueryable 集合根据排序条件进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, SortConditionDto sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 集合根据排序条件进行排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, SortConditionDto<T> sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 已排序集合根据键名称进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> source, string keyName, SortDirectionEnum sortDirection)
    {
        var keySelectorExpression = SortConditionParser<T>.GetSortConditionParser(keyName);
        var keySelector = keySelectorExpression;

        return sortDirection == SortDirectionEnum.Asc
            ? source.ThenBy(keySelector)
            : source.ThenByDescending(keySelector);
    }

    /// <summary>
    /// 对 IQueryable 已排序集合根据排序条件进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> source, SortConditionDto sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 已排序集合根据排序条件进行后续排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> source, SortConditionDto<T> sortCondition)
    {
        var sortField = sortCondition.SortField;
        var sortDirection = sortCondition.SortDirection;
        return ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 IQueryable 集合根据排序条件进行多条件排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortConditions">多排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, IEnumerable<SortConditionDto> sortConditions)
    {
        // 按优先级升序排列排序条件
        sortConditions = sortConditions.OrderBy(x => x.SortPriority);

        // 按优先级依次应用排序
        var firstCondition = sortConditions.First();
        var orderedSource = OrderBy(source, firstCondition);

        foreach (var sortCondition in sortConditions.Skip(1))
        {
            orderedSource = ThenBy(orderedSource, sortCondition);
        }

        return orderedSource;
    }

    /// <summary>
    /// 对 IQueryable 集合根据排序条件进行多条件排序
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="sortConditions">多排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy(IQueryable<T> source, IEnumerable<SortConditionDto<T>> sortConditions)
    {
        // 按优先级升序排列排序条件
        sortConditions = sortConditions.OrderBy(x => x.SortPriority);

        // 按优先级依次应用排序
        var firstCondition = sortConditions.First();
        var orderedSource = OrderBy(source, firstCondition);

        foreach (var sortCondition in sortConditions.Skip(1))
        {
            orderedSource = ThenBy(orderedSource, sortCondition);
        }

        return orderedSource;
    }

    #endregion IQueryable
}
