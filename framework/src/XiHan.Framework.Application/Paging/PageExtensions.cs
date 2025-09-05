#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageExtensions
// Guid:d95f1fd8-ba4d-4932-b63d-23b6a7d6382b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/3 2:09:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Paging.Dtos;
using XiHan.Framework.Application.Paging.Enums;
using XiHan.Framework.Application.Paging.Handlers;

namespace XiHan.Framework.Application.Paging;

/// <summary>
/// 分页扩展方法
/// </summary>
public static class PageExtensions
{
    #region IEnumerable

    #region 选择

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectField">查询字段</param>
    /// <param name="criteriaValue">查询值</param>
    /// <param name="selectCompare">查询比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, string selectField, object? criteriaValue, SelectCompare selectCompare = SelectCompare.Equal)
    {
        return CollectionPropertySelector<T>.Where(source, selectField, criteriaValue, selectCompare);
    }

    /// <summary>
    /// 对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, SelectCondition selectCondition)
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
    public static IEnumerable<T> WhereMultiple<T>(this IEnumerable<T> source, IEnumerable<SelectCondition> selectConditions)
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
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, string sortField, SortDirection sortDirection)
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
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, SortCondition sortCondition)
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
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, string sortField, SortDirection sortDirection)
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
    public static IOrderedEnumerable<T> ThenBy<T>(this IOrderedEnumerable<T> source, SortCondition sortCondition)
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
    public static IOrderedEnumerable<T> OrderByMultiple<T>(this IEnumerable<T> source, IEnumerable<SortCondition> sortConditions)
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

    #region 分页

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    /// <returns>分页后的 List 数据</returns>
    public static List<T> ToPageList<T>(this IEnumerable<T> entities, int currentIndex, int pageSize, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return [.. entities.Skip((currentIndex - defaultFirstIndex) * pageSize).Take(pageSize)];
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    /// <returns>分页后的 List 数据</returns>
    public static List<T> ToPageList<T>(this IEnumerable<T> entities, PageInfo pageInfo, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return [.. entities.Skip((pageInfo.CurrentIndex - defaultFirstIndex) * pageInfo.PageSize).Take(pageInfo.PageSize)];
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// 只返回分页信息
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页数据</returns>
    public static PageData ToPageData<T>(this IEnumerable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageData = new PageData(new PageInfo(currentIndex, pageSize), entities.Count());
        return pageData;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// 只返回分页信息
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <returns>分页数据</returns>
    public static PageData ToPageData<T>(this IEnumerable<T> entities, PageInfo pageInfo)
        where T : class, new()
    {
        var pageData = new PageData(pageInfo, entities.Count());
        return pageData;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="isOnlyPage">是否只返回分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IEnumerable<T> entities, int currentIndex, int pageSize, bool isOnlyPage = false)
        where T : class, new()
    {
        var enumerable = entities as T[] ?? [.. entities];
        var pageDta = enumerable.ToPageData(currentIndex, pageSize);

        if (isOnlyPage)
        {
            return new PageResponseDto<T>(pageDta);
        }

        var responseData = enumerable.ToPageList(currentIndex, pageSize);
        var pageResponse = new PageResponseDto<T>(pageDta, responseData);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="isOnlyPage">是否只返回分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IEnumerable<T> entities, PageInfo pageInfo, bool isOnlyPage = false)
        where T : class, new()
    {
        var enumerable = entities as T[] ?? [.. entities];
        var pageDta = enumerable.ToPageData(pageInfo);

        if (isOnlyPage)
        {
            return new PageResponseDto<T>(pageDta);
        }

        var responseData = enumerable.ToPageList(pageInfo);
        var pageResponse = new PageResponseDto<T>(pageDta, responseData);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">数据源</param>
    /// <param name="queryDto">分页查询</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IEnumerable<T> source, PageQuery queryDto)
        where T : class, new()
    {
        var isQueryAll = queryDto.IsQueryAll ?? false;
        var isOnlyPage = queryDto.IsOnlyPage ?? false;
        var pageInfo = queryDto.PageInfo ?? new PageInfo();

        // 处理查询所有数据的情况
        if (isQueryAll)
        {
            return source.ToPageResponse(pageInfo, isOnlyPage);
        }

        // 处理选择条件
        if (queryDto.SelectConditions is not null)
        {
            source = source.WhereMultiple(queryDto.SelectConditions);
        }

        // 处理排序条件
        if (queryDto.SortConditions is not null)
        {
            source = source.OrderByMultiple(queryDto.SortConditions);
        }

        return source.ToPageResponse(pageInfo, isOnlyPage);
    }

    #endregion 分页

    #endregion IEnumerable

    #region IQueryable

    #region 选择

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectField">查询字段</param>
    /// <param name="criteriaValue">查询值</param>
    /// <param name="selectCompare">查询比较</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, string selectField, object? criteriaValue, SelectCompare selectCompare = SelectCompare.Equal)
    {
        return CollectionPropertySelector<T>.Where(source, selectField, criteriaValue, selectCompare);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectCondition">查询条件</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, SelectCondition selectCondition)
    {
        return CollectionPropertySelector<T>.Where(source, selectCondition);
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
        return CollectionPropertySelector<T>.Where(source, selectCondition);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件选择
    /// </summary>
    /// <param name="source">要应用选择的查询对象</param>
    /// <param name="selectConditions">查询条件</param>
    /// <returns>基于 <paramref name="selectConditions"/> 的选择或未选择的查询对象</returns>
    public static IQueryable<T> WhereMultiple<T>(this IQueryable<T> source, IEnumerable<SelectCondition> selectConditions)
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

    #endregion 选择

    #region 排序

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, string sortField, SortDirection sortDirection)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">原始数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, SortCondition sortCondition)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortCondition);
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
        return CollectionPropertySortor<T>.OrderBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">已排序的数据源</param>
    /// <param name="sortField">排序字段</param>
    /// <param name="sortDirection">排序方向</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, string sortField, SortDirection sortDirection)
    {
        return CollectionPropertySortor<T>.ThenBy(source, sortField, sortDirection);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行后续排序
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">已排序的数据源</param>
    /// <param name="sortCondition">排序条件</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, SortCondition sortCondition)
    {
        return CollectionPropertySortor<T>.ThenBy(source, sortCondition);
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
        return CollectionPropertySortor<T>.ThenBy(source, sortCondition);
    }

    /// <summary>
    /// 对 <see cref="IQueryable{T}"/> 进行多条件排序
    /// </summary>
    /// <typeparam name="T">集合中的元素类型</typeparam>
    /// <param name="source">要排序的集合</param>
    /// <param name="sortConditions">排序条件集合</param>
    /// <returns>排序后的数据</returns>
    public static IOrderedQueryable<T> OrderByMultiple<T>(this IQueryable<T> source, IEnumerable<SortCondition> sortConditions)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sortConditions);
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
        return CollectionPropertySortor<T>.OrderBy(source, sortConditions);
    }

    #endregion 排序

    #region 分页

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    /// <returns>分页后的 List 数据</returns>
    public static List<T> ToPageList<T>(this IQueryable<T> entities, int currentIndex, int pageSize, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return [.. entities.Skip((currentIndex - defaultFirstIndex) * pageSize).Take(pageSize)];
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    public static List<T> ToPageList<T>(this IQueryable<T> entities, PageInfo pageInfo, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return [.. entities.Skip((pageInfo.CurrentIndex - defaultFirstIndex) * pageInfo.PageSize).Take(pageInfo.PageSize)];
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 只返回分页信息
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页数据</returns>
    public static PageData ToPageData<T>(this IQueryable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageData = new PageData(new PageInfo(currentIndex, pageSize), entities.Count());
        return pageData;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 只返回分页信息
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <returns>分页数据</returns>
    public static PageData ToPageData<T>(this IQueryable<T> entities, PageInfo pageInfo)
        where T : class, new()
    {
        var pageData = new PageData(pageInfo, entities.Count());
        return pageData;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="isOnlyPage">是否只返回分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IQueryable<T> entities, int currentIndex, int pageSize, bool isOnlyPage = false)
        where T : class, new()
    {
        var pageDta = entities.ToPageData(currentIndex, pageSize);

        if (isOnlyPage)
        {
            return new PageResponseDto<T>(pageDta);
        }

        var responseData = entities.ToPageList(currentIndex, pageSize);
        var pageResponse = new PageResponseDto<T>(pageDta, responseData);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="isOnlyPage">是否只返回分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IQueryable<T> entities, PageInfo pageInfo, bool isOnlyPage = false)
        where T : class, new()
    {
        var pageDta = entities.ToPageData(pageInfo);

        if (isOnlyPage)
        {
            return new PageResponseDto<T>(pageDta);
        }

        var responseData = entities.ToPageList(pageInfo);
        var pageResponse = new PageResponseDto<T>(pageDta, responseData);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">数据源</param>
    /// <param name="queryDto">分页查询</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPageResponse<T>(this IQueryable<T> source, PageQuery queryDto)
        where T : class, new()
    {
        var isQueryAll = queryDto.IsQueryAll ?? false;
        var isOnlyPage = queryDto.IsOnlyPage ?? false;
        var pageInfo = queryDto.PageInfo ?? new PageInfo();

        // 处理查询所有数据的情况
        if (isQueryAll)
        {
            return source.ToPageResponse(pageInfo, isOnlyPage);
        }

        // 处理选择条件
        if (queryDto.SelectConditions is not null)
        {
            source = source.WhereMultiple(queryDto.SelectConditions);
        }

        // 处理排序条件
        if (queryDto.SortConditions is not null)
        {
            source = source.OrderByMultiple(queryDto.SortConditions);
        }

        return source.ToPageResponse(pageInfo, isOnlyPage);
    }

    #endregion 分页

    #endregion IQueryable
}
