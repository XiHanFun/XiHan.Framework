// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 分页请求元数据
/// </summary>
public sealed class PageRequestMetadata
{
    /// <summary>
    /// 默认页码
    /// </summary>
    public const int DefaultPageIndex = 1;

    /// <summary>
    /// 默认每页大小
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// 最小每页大小
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// 最大每页大小
    /// </summary>
    public const int MaxPageSize = 500;

    private int _pageIndex = DefaultPageIndex;
    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PageRequestMetadata()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    public PageRequestMetadata(int pageIndex, int pageSize)
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
        set => _pageIndex = value < DefaultPageIndex ? DefaultPageIndex : value;
    }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < MinPageSize => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}
