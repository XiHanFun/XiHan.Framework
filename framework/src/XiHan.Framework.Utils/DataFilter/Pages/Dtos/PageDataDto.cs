#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageDataDto
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 7:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DataFilter.Pages.Dtos;

/// <summary>
/// 通用分页数据响应基类
/// </summary>
public class PageDataDto
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageDataDto()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="totalCount"></param>
    public PageDataDto(int totalCount)
    {
        var page = new PageInfoDto();
        PageInfo = page;
        TotalCount = totalCount;
        PageCount = (int)Math.Ceiling((decimal)totalCount / page.PageSize);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="page"></param>
    /// <param name="totalCount"></param>
    public PageDataDto(PageInfoDto page, int totalCount)
    {
        PageInfo = page;
        TotalCount = totalCount;
        PageCount = (int)Math.Ceiling((decimal)totalCount / page.PageSize);
    }

    /// <summary>
    /// 分页数据
    /// </summary>
    public PageInfoDto PageInfo { get; set; } = new();

    /// <summary>
    /// 数据总数
    /// </summary>
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// 总页数
    /// </summary>
    public int PageCount { get; set; } = 1;
}
