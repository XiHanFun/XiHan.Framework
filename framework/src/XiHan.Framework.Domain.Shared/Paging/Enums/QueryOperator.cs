#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryOperator
// Guid:ed708176-466d-46db-8b73-3fff5f658c33
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 06:34:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Domain.Shared.Paging.Enums;

/// <summary>
/// 查询操作
/// </summary>
public enum QueryOperator
{
    #region 基础比较（单值）

    /// <summary>
    /// 等于
    /// </summary>
    [Description("等于")]
    Equal = 1000,

    /// <summary>
    /// 不等于
    /// </summary>
    [Description("不等于")]
    NotEqual = 1001,

    /// <summary>
    /// 大于
    /// </summary>
    [Description("大于")]
    GreaterThan = 1002,

    /// <summary>
    /// 大于等于
    /// </summary>
    [Description("大于等于")]
    GreaterThanOrEqual = 1003,

    /// <summary>
    /// 小于
    /// </summary>
    [Description("小于")]
    LessThan = 1004,

    /// <summary>
    /// 小于等于
    /// </summary>
    [Description("小于等于")]
    LessThanOrEqual = 1005,

    #endregion

    #region 字符串匹配

    /// <summary>
    /// 包含（LIKE %x%）
    /// </summary>
    [Description("包含")]
    Contains = 2000,

    /// <summary>
    /// 以...开始（LIKE x%）
    /// </summary>
    [Description("以...开始")]
    StartsWith = 2001,

    /// <summary>
    /// 以...结束（LIKE %x）
    /// </summary>
    [Description("以...结束")]
    EndsWith = 2002,

    #endregion

    #region 集合比较

    /// <summary>
    /// 在集合中（IN）
    /// </summary>
    [Description("在集合中")]
    In = 3000,

    /// <summary>
    /// 不在集合中（NOT IN）
    /// </summary>
    [Description("不在集合中")]
    NotIn = 3001,

    #endregion

    #region 区间 / 范围

    /// <summary>
    /// 在区间内（Between）
    /// </summary>
    [Description("在区间内")]
    Between = 4000,

    #endregion

    #region 空值判断

    /// <summary>
    /// 为空
    /// </summary>
    [Description("为空")]
    IsNull = 5000,

    /// <summary>
    /// 不为空
    /// </summary>
    [Description("不为空")]
    IsNotNull = 5001,

    #endregion
}
