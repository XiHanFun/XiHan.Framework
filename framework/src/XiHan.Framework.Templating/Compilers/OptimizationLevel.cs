// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
