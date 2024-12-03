#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryableExtensions
// Guid:4e7428c9-1d66-42b2-a7a4-41f1a11b10e8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:19:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Pages;
using XiHan.Framework.Utils.DataFilter.Pages.Dtos;
using XiHan.Framework.Utils.DataFilter.Pages.Enums;

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 可查询扩展方法
/// </summary>
public static class QueryableExtensions
{
    #region 选择

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectField">查询字段</param>
    /// <param name="criteriaValue">查询值</param>
    /// <param name="selectCompare">查询比较</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, string selectField, object criteriaValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        return CollectionPropertySelector<T>.Where(source, selectField, criteriaValue, selectCompare);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, SelectConditionDto selectCondition)
    {
        return CollectionPropertySelector<T>.Where(source, selectCondition.SelectField, selectCondition.SelectCompare);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, SelectConditionDto<T> selectCondition)
        where T : class
    {
        return CollectionPropertySelector<T>.Where(source, selectCondition.SelectField, selectCondition.SelectCompare);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectConditions">查询条件</param>
    /// <returns>基于 <paramref name="selectConditions"/> 的选择或未选择的查询对象</returns>
    public static IQueryable<T> WhereMultiple<T>(this IQueryable<T> source, IEnumerable<SelectConditionDto> selectConditions)
    {
        return CollectionPropertySelector<T>.Where(source, selectConditions);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectConditions">查询条件</param>
    /// <returns>基于 <paramref name="selectConditions"/> 的选择或未选择的查询对象</returns>
    public static IQueryable<T> WhereMultiple<T>(this IQueryable<T> source, IEnumerable<SelectConditionDto<T>> selectConditions)
        where T : class
    {
        return CollectionPropertySelector<T>.Where(source, selectConditions);
    }

    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择查询对象的谓词</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的查询对象</returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    #endregion

    #region 排序

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, string sortField, SortDirectionEnum sortDirection)
    {
        return CollectionPropertySorter<T>.OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, SortConditionDto sortCondition)
    {
        return CollectionPropertySorter<T>.OrderBy(source, sortCondition.SortField, sortCondition.SortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, SortConditionDto<T> sortCondition)
        where T : class
    {
        return CollectionPropertySorter<T>.OrderBy(source, sortCondition.SortField, sortCondition.SortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">已排序的数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, string sortField, SortDirectionEnum sortDirection)
    {
        return CollectionPropertySorter<T>.ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">已排序的数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, SortConditionDto sortCondition)
    {
        return CollectionPropertySorter<T>.ThenBy(source, sortCondition.SortField, sortCondition.SortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">已排序的数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, SortConditionDto<T> sortCondition)
        where T : class
    {
        return CollectionPropertySorter<T>.ThenBy(source, sortCondition.SortField, sortCondition.SortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件排序
    /// </summary>
    /// <typeparam name="T">集合中的元素类型</typeparam>
    /// <param name="source">要排序的集合</param>
    /// <param name="sortConditions">排序条件集合</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderByMultiple<T>(this IQueryable<T> source, IEnumerable<SortConditionDto> sortConditions)
    {
        return CollectionPropertySorter<T>.OrderBy(source, sortConditions);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件排序
    /// </summary>
    /// <typeparam name="T">集合中的元素类型</typeparam>
    /// <param name="source">要排序的集合</param>
    /// <param name="sortConditions">排序条件集合</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderByMultiple<T>(this IQueryable<T> source, IEnumerable<SortConditionDto<T>> sortConditions)
        where T : class
    {
        return CollectionPropertySorter<T>.OrderBy(source, sortConditions);
    }

    #endregion
}
