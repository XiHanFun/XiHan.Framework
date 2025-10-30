#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectCompareEnum
// Guid:ed708176-466d-46db-8b73-3fff5f658c33
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:34:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Domain.Paging.Enums;

/// <summary>
/// 选择比较枚举
/// </summary>
public enum SelectCompare
{
    #region 单值比较

    /// <summary>
    /// 等于
    /// </summary>
    [Description("等于")]
    Equal,

    /// <summary>
    /// 大于
    /// </summary>
    [Description("大于")]
    Greater,

    /// <summary>
    /// 大于等于
    /// </summary>
    [Description("大于等于")]
    GreaterEqual,

    /// <summary>
    /// 小于
    /// </summary>
    [Description("小于")]
    Less,

    /// <summary>
    /// 小于等于
    /// </summary>
    [Description("小于等于")]
    LessEqual,

    /// <summary>
    /// 不等于
    /// </summary>
    [Description("不等于")]
    NotEqual,

    #endregion 单值比较

    #region 集合比较

    /// <summary>
    /// 包含
    /// </summary>
    [Description("包含")]
    Contains,

    /// <summary>
    /// 多值包含比较
    /// </summary>
    [Description("多值包含比较")]
    InWithContains,

    /// <summary>
    /// 多值等于比较
    /// </summary>
    [Description("多值等于比较")]
    InWithEqual,

    #endregion 集合比较

    #region 区间比较

    /// <summary>
    /// 在于
    /// </summary>
    [Description("在于")]
    Between

    #endregion 区间比较
}
