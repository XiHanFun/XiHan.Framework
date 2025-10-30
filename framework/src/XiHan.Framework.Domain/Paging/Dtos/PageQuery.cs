#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageQuery
// Guid:aec9055a-15c5-48d1-a835-05a28e11cdf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 7:11:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Paging.Dtos;

/// <summary>
/// 分页查询请求
/// </summary>
public class PageQuery
{
    private int _pageIndex = PageInfo.DefaultPageIndex;

    private int _pageSize = PageInfo.DefaultPageSize;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PageQuery()
    {
        PageIndex = PageInfo.DefaultPageIndex;
        PageSize = PageInfo.DefaultPageSize;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    public PageQuery(int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < PageInfo.DefaultPageIndex ? PageInfo.DefaultPageIndex : value;
    }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < PageInfo.MinPageSize => PageInfo.DefaultPageSize,
            > PageInfo.MaxPageSize => PageInfo.MaxPageSize,
            _ => value
        };
    }

    /// <summary>
    /// 选择条件集合
    /// </summary>
    public List<SelectCondition>? Filters { get; set; }

    /// <summary>
    /// 排序条件集合
    /// </summary>
    public List<SortCondition>? Sorts { get; set; }

    /// <summary>
    /// 搜索关键字（可选）
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 是否禁用分页（返回所有数据）
    /// </summary>
    public bool DisablePaging { get; set; }

    /// <summary>
    /// 转换为 PageInfo
    /// </summary>
    public PageInfo ToPageInfo() => new(PageIndex, PageSize);
}
