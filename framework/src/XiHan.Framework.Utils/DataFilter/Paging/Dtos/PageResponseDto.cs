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

namespace XiHan.Framework.Utils.DataFilter.Paging.Dtos;

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
    where T : class, new()
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
    public PageResponseDto(PageDataDto pageData)
        : base(pageData)
    {
        ResponseData = null;
        ExtraData = null;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageData"></param>
    /// <param name="responseData"></param>
    public PageResponseDto(PageDataDto pageData, IReadOnlyList<T>? responseData)
        : base(pageData)
    {
        ResponseData = responseData;
        ExtraData = null;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageData"></param>
    /// <param name="responseData"></param>
    /// <param name="extraData"></param>
    public PageResponseDto(PageDataDto pageData, IReadOnlyList<T>? responseData, object extraData)
        : base(pageData)
    {
        ResponseData = responseData;
        ExtraData = extraData;
    }

    /// <summary>
    /// 数据列表（只读）
    /// </summary>
    public IReadOnlyList<T>? ResponseData { get; set; }

    /// <summary>
    /// 扩展数据（只读）
    /// </summary>
    public object? ExtraData { get; set; }
}
