#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageExtensions
// Guid:d15a02f4-f5a4-475c-b7f7-04365d792348
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/29 7:38:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Dtos;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 分页扩展
/// </summary>
public static class PageExtensions
{
    /// <summary>
    /// 数据分页
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="page"></param>
    /// <param name="defaultFirstIndex"></param>
    /// <returns></returns>
    public static IQueryable<T> ToPage<T>(this IQueryable<T> query, PageInfoDto page, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return query.Skip((page.CurrentIndex - defaultFirstIndex) * page.PageSize).Take(page.PageSize);
    }

    /// <summary>
    /// 数据选择分页
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="page"></param>
    /// <param name="selectConditions"></param>
    /// <returns></returns>
    public static IQueryable<T> ToPage<T>(this IQueryable<T> query, PageInfoDto page, IEnumerable<SelectConditionDto> selectConditions)
        where T : class, new()
    {
        return query.Where(selectConditions).ToPage(page);
    }

    /// <summary>
    /// 数据排序分页
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="page"></param>
    /// <param name="sortConditions"></param>
    /// <returns></returns>
    public static IQueryable<T> ToPage<T>(this IQueryable<T> query, PageInfoDto page, IEnumerable<SortConditionDto> sortConditions)
        where T : class, new()
    {
        return query.ToOrder(sortConditions).ToPage(page);
    }

    /// <summary>
    /// 数据查询分页
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="page"></param>
    /// <param name="selectConditions"></param>
    /// <param name="sortConditions"></param>
    /// <returns></returns>
    public static IQueryable<T> ToPage<T>(this IQueryable<T> query, PageInfoDto page, IEnumerable<SelectConditionDto> selectConditions, IEnumerable<SortConditionDto> sortConditions)
        where T : class, new()
    {
        return query.ToQuery(selectConditions, sortConditions).ToPage(page);
    }
}
