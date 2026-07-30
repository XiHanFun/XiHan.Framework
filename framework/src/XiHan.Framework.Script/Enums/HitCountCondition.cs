// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
