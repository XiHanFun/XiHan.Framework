#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageResponse
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 分页响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class PageResponse<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageResponse()
    {
        Items = [];
        PageData = new PageData();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    public PageResponse(IReadOnlyList<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageData = new PageData(pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">数据项</param>
    /// <param name="pageData">分页元数据</param>
    public PageResponse(IReadOnlyList<T> items, PageData pageData)
    {
        Items = items;
        PageData = pageData;
    }

    /// <summary>
    /// 数据项列表
    /// </summary>
    public IReadOnlyList<T> Items { get; set; }

    /// <summary>
    /// 分页元数据
    /// </summary>
    public PageData PageData { get; set; }

    /// <summary>
    /// 扩展数据（可选）
    /// </summary>
    public Dictionary<string, object>? ExtendData { get; set; }
}
