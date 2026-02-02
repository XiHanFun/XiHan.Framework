#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageHelper
// Guid:2b3c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Helpers;

/// <summary>
/// 分页辅助类
/// </summary>
public static class PageHelper
{
    /// <summary>
    /// 计算总页数
    /// </summary>
    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        if (pageSize <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling((double)totalCount / pageSize);
    }

    /// <summary>
    /// 计算跳过的记录数
    /// </summary>
    public static int CalculateSkip(int pageIndex, int pageSize)
    {
        var validPageIndex = Math.Max(pageIndex, PageRequestMetadata.DefaultPageIndex);
        return (validPageIndex - 1) * pageSize;
    }

    /// <summary>
    /// 验证页码是否有效
    /// </summary>
    public static bool IsValidPageIndex(int pageIndex, int totalPages)
    {
        return pageIndex >= PageRequestMetadata.DefaultPageIndex && pageIndex <= Math.Max(totalPages, 1);
    }

    /// <summary>
    /// 修正页码（确保在有效范围内）
    /// </summary>
    public static int FixPageIndex(int pageIndex, int totalPages)
    {
        if (pageIndex < PageRequestMetadata.DefaultPageIndex)
        {
            return PageRequestMetadata.DefaultPageIndex;
        }

        if (totalPages > 0 && pageIndex > totalPages)
        {
            return totalPages;
        }

        return pageIndex;
    }

    /// <summary>
    /// 修正每页大小（确保在有效范围内）
    /// </summary>
    public static int FixPageSize(int pageSize)
    {
        return pageSize switch
        {
            < PageRequestMetadata.MinPageSize => PageRequestMetadata.DefaultPageSize,
            > PageRequestMetadata.MaxPageSize => PageRequestMetadata.MaxPageSize,
            _ => pageSize
        };
    }

    /// <summary>
    /// 计算起始记录号（从1开始）
    /// </summary>
    public static int CalculateStartRecord(int pageIndex, int pageSize, int totalCount)
    {
        if (totalCount == 0)
        {
            return 0;
        }

        return (pageIndex - 1) * pageSize + 1;
    }

    /// <summary>
    /// 计算结束记录号
    /// </summary>
    public static int CalculateEndRecord(int pageIndex, int pageSize, int totalCount)
    {
        if (totalCount == 0)
        {
            return 0;
        }

        return Math.Min(pageIndex * pageSize, totalCount);
    }

    /// <summary>
    /// 获取分页摘要信息
    /// </summary>
    public static string GetPageSummary(int pageIndex, int pageSize, int totalCount)
    {
        if (totalCount == 0)
        {
            return "暂无数据";
        }

        var startRecord = CalculateStartRecord(pageIndex, pageSize, totalCount);
        var endRecord = CalculateEndRecord(pageIndex, pageSize, totalCount);
        var totalPages = CalculateTotalPages(totalCount, pageSize);

        return $"第 {startRecord}-{endRecord} 条，共 {totalCount} 条记录，第 {pageIndex}/{totalPages} 页";
    }

    /// <summary>
    /// 获取分页范围（用于生成页码列表）
    /// </summary>
    /// <param name="currentPage">当前页码</param>
    /// <param name="totalPages">总页数</param>
    /// <param name="rangeSize">范围大小（当前页前后显示的页数）</param>
    public static List<int> GetPageRange(int currentPage, int totalPages, int rangeSize = 2)
    {
        if (totalPages <= 0)
        {
            return [];
        }

        var startPage = Math.Max(1, currentPage - rangeSize);
        var endPage = Math.Min(totalPages, currentPage + rangeSize);

        var pages = new List<int>();
        for (var i = startPage; i <= endPage; i++)
        {
            pages.Add(i);
        }

        return pages;
    }

    /// <summary>
    /// 判断是否有上一页
    /// </summary>
    public static bool HasPrevious(int pageIndex) => pageIndex > PageRequestMetadata.DefaultPageIndex;

    /// <summary>
    /// 判断是否有下一页
    /// </summary>
    public static bool HasNext(int pageIndex, int totalPages) => pageIndex < totalPages;

    /// <summary>
    /// 判断是否是第一页
    /// </summary>
    public static bool IsFirstPage(int pageIndex) => pageIndex == PageRequestMetadata.DefaultPageIndex;

    /// <summary>
    /// 判断是否是最后一页
    /// </summary>
    public static bool IsLastPage(int pageIndex, int totalPages) => pageIndex >= totalPages;

    /// <summary>
    /// 创建默认分页元数据
    /// </summary>
    public static PageRequestMetadata CreateDefaultMetadata()
    {
        return new PageRequestMetadata(
            PageRequestMetadata.DefaultPageIndex,
            PageRequestMetadata.DefaultPageSize);
    }

    /// <summary>
    /// 创建分页元数据
    /// </summary>
    public static PageRequestMetadata CreateMetadata(int pageIndex, int pageSize)
    {
        var fixedPageIndex = Math.Max(pageIndex, PageRequestMetadata.DefaultPageIndex);
        var fixedPageSize = FixPageSize(pageSize);

        return new PageRequestMetadata(fixedPageIndex, fixedPageSize);
    }
}
