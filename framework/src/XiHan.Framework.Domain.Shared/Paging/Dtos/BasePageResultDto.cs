#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BasePageResultDto
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 通用分页响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class BasePageResultDto<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public BasePageResultDto()
    {
        Items = [];
        PageResultMetadata = new PageResultMetadata();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    public BasePageResultDto(IList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageResultMetadata = new PageResultMetadata(pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageData">分页元数据</param>
    public BasePageResultDto(IList<T> items, PageResultMetadata pageData)
    {
        Items = items;
        PageResultMetadata = pageData;
    }

    /// <summary>
    /// 数据项列表
    /// </summary>
    public IList<T> Items { get; set; }

    /// <summary>
    /// 分页响应元数据
    /// </summary>
    public PageResultMetadata PageResultMetadata { get; set; }

    /// <summary>
    /// 扩展数据（可选）
    /// </summary>
    public Dictionary<string, object>? ExtendDatas { get; set; }

    /// <summary>
    /// 创建空结果
    /// </summary>
    public static BasePageResultDto<T> Empty(int pageIndex = PageRequestMetadata.DefaultPageIndex,
        int pageSize = PageRequestMetadata.DefaultPageSize)
    {
        return new BasePageResultDto<T>([], pageIndex, pageSize, 0);
    }

    /// <summary>
    /// 从已有数据创建结果
    /// </summary>
    public static BasePageResultDto<T> Create(IList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        return new BasePageResultDto<T>(items, pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 从请求和数据创建结果
    /// </summary>
    public static BasePageResultDto<T> Create(IList<T> items, BasePageRequestDto request, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new BasePageResultDto<T>(items, request.PageIndex, request.PageSize, totalCount);
    }

    /// <summary>
    /// 映射为其他类型的分页结果
    /// </summary>
    public BasePageResultDto<TTarget> Map<TTarget>(Func<T, TTarget> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        var mappedItems = Items.Select(mapper).ToList();
        return new BasePageResultDto<TTarget>(mappedItems, PageResultMetadata);
    }
}
