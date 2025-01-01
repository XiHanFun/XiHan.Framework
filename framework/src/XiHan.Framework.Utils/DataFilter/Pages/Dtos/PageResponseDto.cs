#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageResponseDto
// Guid:373148b9-8be2-41a0-b971-82ef6750800d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 7:12:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DataFilter.Pages.Dtos;

/// <summary>
/// 通用分页响应基类
/// </summary>
public class PageResponseDto
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public PageResponseDto()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageData"></param>
    public PageResponseDto(PageDataDto pageData)
    {
        PageData = pageData;
    }

    /// <summary>
    /// 分页数据
    /// </summary>
    public PageDataDto PageData { get; set; } = new();
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
    public PageResponseDto()
        : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageData"></param>
    /// <param name="responseDatas"></param>
    public PageResponseDto(PageDataDto pageData, IList<T>? responseDatas)
        : base(pageData)
    {
        ResponseDatas = responseDatas;
    }

    /// <summary>
    /// 数据
    /// </summary>
    public IList<T>? ResponseDatas { get; set; }
}
