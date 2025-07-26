#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ComparisonOperator
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Enums;

/// <summary>
/// 比较操作符
/// </summary>
public enum ComparisonOperator
{
    /// <summary>
    /// 等于
    /// </summary>
    Equal = 0,

    /// <summary>
    /// 不等于
    /// </summary>
    NotEqual = 1,

    /// <summary>
    /// 大于
    /// </summary>
    GreaterThan = 2,

    /// <summary>
    /// 大于等于
    /// </summary>
    GreaterThanOrEqual = 3,

    /// <summary>
    /// 小于
    /// </summary>
    LessThan = 4,

    /// <summary>
    /// 小于等于
    /// </summary>
    LessThanOrEqual = 5,

    /// <summary>
    /// 包含
    /// </summary>
    Contains = 6,

    /// <summary>
    /// 不包含
    /// </summary>
    NotContains = 7,

    /// <summary>
    /// 开始于
    /// </summary>
    StartsWith = 8,

    /// <summary>
    /// 结束于
    /// </summary>
    EndsWith = 9,

    /// <summary>
    /// 在范围内
    /// </summary>
    In = 10,

    /// <summary>
    /// 不在范围内
    /// </summary>
    NotIn = 11,

    /// <summary>
    /// 为空
    /// </summary>
    IsNull = 12,

    /// <summary>
    /// 不为空
    /// </summary>
    IsNotNull = 13
}
