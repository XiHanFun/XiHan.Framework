#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageResultDtoBase
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 分页响应基类
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class PageResultDtoBase<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageResultDtoBase()
    {
        Items = [];
        Page = new PageResultMetadata();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    public PageResultDtoBase(IList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        Page = new PageResultMetadata(pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageData">分页元数据</param>
    public PageResultDtoBase(IList<T> items, PageResultMetadata pageData)
    {
        Items = items;
        Page = pageData;
    }

    /// <summary>
    /// 数据项列表
    /// </summary>
    public IList<T> Items { get; set; }

    /// <summary>
    /// 分页响应元数据
    /// </summary>
    public PageResultMetadata Page { get; set; }

    /// <summary>
    /// 扩展数据（可选）
    /// </summary>
    public Dictionary<string, object>? ExtendDatas { get; set; }

    /// <summary>
    /// 创建空结果
    /// </summary>
    public static PageResultDtoBase<T> Empty(int pageIndex = PageRequestMetadata.DefaultPageIndex,
        int pageSize = PageRequestMetadata.DefaultPageSize)
    {
        return new PageResultDtoBase<T>([], pageIndex, pageSize, 0);
    }

    /// <summary>
    /// 从已有数据创建结果
    /// </summary>
    public static PageResultDtoBase<T> Create(IList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        return new PageResultDtoBase<T>(items, pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 从请求和数据创建结果
    /// </summary>
    public static PageResultDtoBase<T> Create(IList<T> items, PageRequestDtoBase request, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PageResultDtoBase<T>(items, request.Page.PageIndex, request.Page.PageSize, totalCount);
    }

    /// <summary>
    /// 映射为其他类型的分页结果
    /// </summary>
    public PageResultDtoBase<TTarget> Map<TTarget>(Func<T, TTarget> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var mappedItems = Items.Select(mapper).ToList();
        return new PageResultDtoBase<TTarget>(mappedItems, Page);
    }
}
