// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 求值结果
/// </summary>
/// <remarks>
/// 子句四种都可能；组与条件集只会是命中、不命中或待定（「无法判定」在组里按不命中计）。
/// </remarks>
public enum ConditionOutcome
{
    /// <summary>
    /// 命中
    /// </summary>
    [Description("命中")]
    Matched = 0,

    /// <summary>
    /// 不命中
    /// </summary>
    [Description("不命中")]
    NotMatched = 1,

    /// <summary>
    /// 无法判定：属性取不到、值转换失败或子句编译出错；按不命中计，取反不翻转
    /// </summary>
    [Description("无法判定")]
    Indeterminate = 2,

    /// <summary>
    /// 待定：属性暂不可得，补齐后再求一次
    /// </summary>
    [Description("待定")]
    Pending = 3
}
