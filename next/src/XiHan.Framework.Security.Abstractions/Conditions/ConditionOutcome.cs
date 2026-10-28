// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 求值结果。子句四种都可能；组与条件集只会是命中、不命中或待定（无法判定在组里按不命中计）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ConditionOutcome
{
    /// <summary>
    /// 命中。
    /// </summary>
    Matched = 0,

    /// <summary>
    /// 不命中。
    /// </summary>
    NotMatched = 1,

    /// <summary>
    /// 无法判定：属性取不到、值转换失败或子句编译出错；按不命中计，取反不翻转。
    /// </summary>
    Indeterminate = 2,

    /// <summary>
    /// 待定：属性暂不可得，补齐后再求一次。
    /// </summary>
    Pending = 3,
}
