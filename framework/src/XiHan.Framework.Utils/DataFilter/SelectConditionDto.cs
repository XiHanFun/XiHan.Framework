#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectConditionDto
// Guid:67b10bd1-1623-4f95-af56-19a45b4390c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:33:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 通用选择条件
/// </summary>
public class SelectConditionDto
{
    /// <summary>
    /// 选择字段
    /// </summary>
    public string SelectField { get; set; } = string.Empty;

    /// <summary>
    /// 字段值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 值类型
    /// </summary>
    public string ValueType { get; set; } = string.Empty;

    /// <summary>
    /// 选择比较
    /// </summary>
    public SelectCompareEnum SelectCompare { get; set; }
}