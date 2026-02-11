#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OptimizationLevel
// Guid:2e254567-808a-45a1-9553-2cee7ba73d71
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:17:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 优化级别
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// 无优化
    /// </summary>
    None = 0,

    /// <summary>
    /// 基础优化
    /// </summary>
    Basic = 1,

    /// <summary>
    /// 标准优化
    /// </summary>
    Standard = 2,

    /// <summary>
    /// 激进优化
    /// </summary>
    Aggressive = 3
}
