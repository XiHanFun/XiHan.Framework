#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageResultMetadata
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 分页响应元数据
/// </summary>
public sealed class PageResultMetadata
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageResultMetadata()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    public PageResultMetadata(int pageIndex, int pageSize, int totalCount)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageIndex { get; set; } = PageRequestMetadata.DefaultPageIndex;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; } = PageRequestMetadata.DefaultPageSize;

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

    /// <summary>
    /// 当前页起始记录号（从1开始）
    /// </summary>
    public int StartRecord => TotalCount == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;

    /// <summary>
    /// 当前页结束记录号
    /// </summary>
    public int EndRecord => Math.Min(PageIndex * PageSize, TotalCount);

    /// <summary>
    /// 当前页实际记录数
    /// </summary>
    public int CurrentPageCount => TotalCount == 0 ? 0 : EndRecord - StartRecord + 1;
}
