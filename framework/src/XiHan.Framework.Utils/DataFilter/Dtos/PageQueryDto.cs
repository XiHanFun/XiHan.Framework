#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageQueryDto
// Guid:aec9055a-15c5-48d1-a835-05a28e11cdf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 7:11:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.DataFilter.Dtos;

/// <summary>
/// 通用分页查询基类
/// </summary>
public class PageQueryDto
{
    /// <summary>
    /// 是否查询所有数据
    /// 是则忽略分页信息，返回所有数据并绑定默认分页信息
    /// </summary>
    public bool? IsQueryAll { get; set; }

    /// <summary>
    /// 是否只返回分页信息
    /// 是则只返回分页信息，否则返回分页信息及结果数据
    /// </summary>
    public bool? IsOnlyPage { get; set; }

    /// <summary>
    /// 分页信息
    /// </summary>
    public PageInfoDto? PageInfo { get; set; }

    /// <summary>
    /// 选择条件集合
    /// </summary>
    public List<SelectConditionDto>? SelectConditions { get; set; }

    /// <summary>
    /// 排序条件集合
    /// </summary>
    public List<SortConditionDto>? SortConditions { get; set; }
}
