#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HitCountCondition
// Guid:d5fe64db-1e23-496a-be9b-3bdc70ad8dd4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/01 11:03:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Enums;

/// <summary>
/// 命中条件
/// </summary>
public enum HitCountCondition
{
    /// <summary>
    /// 总是命中
    /// </summary>
    Always,

    /// <summary>
    /// 等于指定次数
    /// </summary>
    Equal,

    /// <summary>
    /// 大于等于指定次数
    /// </summary>
    GreaterOrEqual,

    /// <summary>
    /// 是指定次数的倍数
    /// </summary>
    Multiple
}
