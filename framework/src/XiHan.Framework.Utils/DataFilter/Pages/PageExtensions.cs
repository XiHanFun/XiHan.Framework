#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageExtensions
// Guid:d95f1fd8-ba4d-4932-b63d-23b6a7d6382b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/3 2:09:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.DataFilter.Pages.Dtos;

namespace XiHan.Framework.Utils.DataFilter.Pages;

/// <summary>
/// 分页扩展方法
/// </summary>
public static class PageExtensions
{
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
        return entities.Skip((currentIndex - defaultFirstIndex) * pageSize).Take(pageSize).ToList();
    }

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
    /// 获取 IEnumerable 分页数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    /// <returns>分页后的 List 数据</returns>
    public static List<T> ToPageList<T>(this IEnumerable<T> entities, PageInfoDto pageInfo, int defaultFirstIndex = 1)
        where T : class, new()
    {
        return entities.Skip((pageInfo.CurrentIndex - defaultFirstIndex) * pageInfo.PageSize).Take(pageInfo.PageSize)
            .ToList();
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <param name="defaultFirstIndex">默认起始下标</param>
    public static List<T> ToPageList<T>(this IQueryable<T> entities, PageInfoDto pageInfo, int defaultFirstIndex = 1)
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
    public static PageDataDto ToPageData<T>(this IEnumerable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageData = new PageDataDto(new PageInfoDto(currentIndex, pageSize), entities.Count());
        return pageData;
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
    public static PageDataDto ToPageData<T>(this IQueryable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageData = new PageDataDto(new PageInfoDto(currentIndex, pageSize), entities.Count());
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
    public static PageDataDto ToPageData<T>(this IEnumerable<T> entities, PageInfoDto pageInfo)
        where T : class, new()
    {
        var pageData = new PageDataDto(pageInfo, entities.Count());
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
    public static PageDataDto ToPageData<T>(this IQueryable<T> entities, PageInfoDto pageInfo)
        where T : class, new()
    {
        var pageData = new PageDataDto(pageInfo, entities.Count());
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
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IEnumerable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageDta = new PageDataDto(new PageInfoDto(currentIndex, pageSize), entities.Count());
        var responseDatas = entities.ToPageList(currentIndex, pageSize);
        var pageResponse = new PageResponseDto<T>(pageDta, responseDatas);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="currentIndex">当前页标</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IQueryable<T> entities, int currentIndex, int pageSize)
        where T : class, new()
    {
        var pageDta = new PageDataDto(new PageInfoDto(currentIndex, pageSize), entities.Count());
        var responseDatas = entities.ToPageList(currentIndex, pageSize);
        var pageResponse = new PageResponseDto<T>(pageDta, responseDatas);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据源类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IEnumerable<T> entities, PageInfoDto pageInfo)
        where T : class, new()
    {
        var pageDta = new PageDataDto(pageInfo, entities.Count());
        var responseDatas = entities.ToPageList(pageInfo);
        var pageResponse = new PageResponseDto<T>(pageDta, responseDatas);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// 返回分页信息和数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="entities">数据源</param>
    /// <param name="pageInfo">分页信息</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IQueryable<T> entities, PageInfoDto pageInfo)
        where T : class, new()
    {
        var pageDta = new PageDataDto(pageInfo, entities.Count());
        var responseDatas = entities.ToPageList(pageInfo);
        var pageResponse = new PageResponseDto<T>(pageDta, responseDatas);
        return pageResponse;
    }

    /// <summary>
    /// 获取 IEnumerable 分页数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">数据源</param>
    /// <param name="queryDto">分页查询</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IEnumerable<T> source, PageQueryDto queryDto)
       where T : class, new()
    {
        // 处理查询所有数据的情况
        if (queryDto.IsQueryAll == true)
        {
            return source.ToPagePageResponse(new PageInfoDto());
        }
        // 处理选择条件
        if (queryDto.SelectConditions != null)
        {
            source = source.WhereMultiple(queryDto.SelectConditions);
        }
        // 处理排序条件
        if (queryDto.SortConditions != null)
        {
            source = source.OrderByMultiple(queryDto.SortConditions);
        }
        // 获取分页信息
        var pageInfo = queryDto.PageInfo ?? new PageInfoDto();
        return source.ToPagePageResponse(pageInfo);
    }

    /// <summary>
    /// 获取 IQueryable 分页数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source">数据源</param>
    /// <param name="queryDto">分页查询</param>
    /// <returns>分页后的分页信息和数据</returns>
    public static PageResponseDto<T> ToPagePageResponse<T>(this IQueryable<T> source, PageQueryDto queryDto)
       where T : class, new()
    {
        // 处理查询所有数据的情况
        if (queryDto.IsQueryAll == true)
        {
            return source.ToPagePageResponse(new PageInfoDto());
        }
        // 处理选择条件
        if (queryDto.SelectConditions != null)
        {
            source = source.WhereMultiple(queryDto.SelectConditions);
        }
        // 处理排序条件
        if (queryDto.SortConditions != null)
        {
            source = source.OrderByMultiple(queryDto.SortConditions);
        }
        // 获取分页信息
        var pageInfo = queryDto.PageInfo ?? new PageInfoDto();
        return source.ToPagePageResponse(pageInfo);
    }
}
