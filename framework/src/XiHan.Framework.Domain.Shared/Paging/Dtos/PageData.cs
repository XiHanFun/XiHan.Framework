#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageData
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 分页响应元数据
/// </summary>
public class PageData
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageData()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    public PageData(int pageIndex, int pageSize, int totalCount)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageIndex { get; set; } = PageInfo.DefaultPageIndex;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; } = PageInfo.DefaultPageSize;

    /// <summary>
    /// 总记录数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// 是否有上一页
    /// </summary>
    public bool HasPrevious => PageIndex > 1;

    /// <summary>
    /// 是否有下一页
    /// </summary>
    public bool HasNext => PageIndex < TotalPages;

    /// <summary>
    /// 是否是第一页
    /// </summary>
    public bool IsFirstPage => PageIndex == 1;

    /// <summary>
    /// 是否是最后一页
    /// </summary>
    public bool IsLastPage => PageIndex >= TotalPages;
}
