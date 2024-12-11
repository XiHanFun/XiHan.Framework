#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumerableExtensions
// Guid:3d50f5ab-2bbb-4643-a8bc-03e137b0428f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 2:33:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Pages.Dtos;
using XiHan.Framework.Utils.DataFilter.Pages.Enums;
using XiHan.Framework.Utils.DataFilter.Pages.Handlers;

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 可列举扩展方法
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// 使用指定的分隔符连接构造的 <see cref="IEnumerable{T}"/> 集合（类型为 System.String）的成员
    /// 这是 string.Join(...) 的快捷方式
    /// </summary>
    /// <param name="source">包含要连接的字符串的集合</param>
    /// <param name="separator">要用作分隔符的字符串只有当 values 有多个元素时，separator 才会包含在返回的字符串中</param>
    /// <returns>由 values 的成员组成的字符串，这些成员由 separator 字符串分隔。如果 values 没有成员，则方法返回 System.String.Empty</returns>
    public static string JoinAsString(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source);
    }

    /// <summary>
    /// 使用指定的分隔符连接集合的成员
    /// 这是 string.Join(...) 的快捷方式
    /// </summary>
    /// <param name="source">包含要连接的对象的集合</param>
    /// <param name="separator">要用作分隔符的字符串只有当 values 有多个元素时，separator 才会包含在返回的字符串中</param>
    /// <typeparam name="T">values 成员的类型</typeparam>
    /// <returns>由 values 的成员组成的字符串，这些成员由 separator 字符串分隔如果 values 没有成员，则方法返回 System.String.Empty</returns>
    public static string JoinAsString<T>(this IEnumerable<T> source, string separator)
    {
        return string.Join(separator, source);
    }

    #region 选择

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectField">查询字段</param>
    /// <param name="criteriaValue">查询值</param>
    /// <param name="selectCompare">查询比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, string selectField, object? criteriaValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        return CollectionPropertySelector<T>.Where(source, selectField, criteriaValue, selectCompare);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, SelectConditionDto selectCondition)
    {
        return CollectionPropertySelector<T>.Where(source, selectCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, SelectConditionDto<T> selectCondition)
        where T : class
    {
        return CollectionPropertySelector<T>.Where(source, selectCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行多条件选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectConditions">查询条件</param>
    /// <returns>基于 <paramref name="selectConditions"/> 的选择或未选择的查询对象</returns>
    public static IEnumerable<T> WhereMultiple<T>(this IEnumerable<T> source, IEnumerable<SelectConditionDto> selectConditions)
    {
        return CollectionPropertySelector<T>.Where(source, selectConditions);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行多条件选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectConditions">查询条件</param>
    /// <returns>基于 <paramref name="selectConditions"/> 的选择或未选择的查询对象</returns>
    public static IEnumerable<T> WhereMultiple<T>(this IEnumerable<T> source, IEnumerable<SelectConditionDto<T>> selectConditions)
        where T : class
    {
        return CollectionPropertySelector<T>.Where(source, selectConditions);
    }

    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的枚举对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择枚举对象的谓词</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的枚举对象</returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的枚举对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择枚举对象的谓词，包含索引</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的枚举对象</returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, int, bool> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    #endregion 选择

    #region 排序

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, string sortField, SortDirectionEnum sortDirection)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, SortConditionDto sortCondition)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, SortConditionDto<T> sortCondition)
        where T : class
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, string sortField, SortDirectionEnum sortDirection)
    {
        return CollectionPropertySortor<T>.ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, SortConditionDto sortCondition)
    {
        return CollectionPropertySortor<T>.ThenBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, SortConditionDto<T> sortCondition)
        where T : class
    {
        return CollectionPropertySortor<T>.ThenBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行多条件排序
    /// </summary>
    /// <typeparam name="T">集合中的元素类型</typeparam>
    /// <param name="source">要排序的集合</param>
    /// <param name="sortConditions">排序条件集合</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderByMultiple<T>(this IEnumerable<T> source, IEnumerable<SortConditionDto> sortConditions)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortConditions);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行多条件排序
    /// </summary>
    /// <typeparam name="T">集合中的元素类型</typeparam>
    /// <param name="source">要排序的集合</param>
    /// <param name="sortConditions">排序条件集合</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedEnumerable<T> OrderByMultiple<T>(this IEnumerable<T> source, IEnumerable<SortConditionDto<T>> sortConditions)
        where T : class
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortConditions);
    }

    #endregion 排序
}
