#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageResponseDto
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 7:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DataFilter.Dtos;

/// <summary>
/// 通用分页响应基类
/// </summary>
public class PageResponseDto
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="page"></param>
    /// <param name="totalCount"></param>
    public PageResponseDto(PageInfoDto page, int totalCount)
    {
        Page = page;
        TotalCount = totalCount;
        PageCount = (int)Math.Ceiling((decimal)totalCount / page.PageSize);
    }

    /// <summary>
    /// 分页数据
    /// </summary>
    public PageInfoDto Page { get; set; }

    /// <summary>
    /// 数据总数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int PageCount { get; set; }
}

/// <summary>
/// 通用分页响应泛型基类
/// </summary>
/// <typeparam name="T"></typeparam>
public class PageResponseDto<T> : PageResponseDto
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="page"></param>
    /// <param name="datas"></param>
    public PageResponseDto(PageInfoDto page, List<T> datas) : base(page, datas.Count)
    {
        Datas = datas;
    }

    /// <summary>
    /// 数据
    /// </summary>
    public List<T> Datas { get; init; }
}
