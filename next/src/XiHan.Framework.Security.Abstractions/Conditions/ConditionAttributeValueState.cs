// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 属性取值状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ConditionAttributeValueState
{
    /// <summary>
    /// 有值（多值属性可以是空列表）。
    /// </summary>
    Present = 0,

    /// <summary>
    /// 取不到。
    /// </summary>
    /// <remarks>
    /// 子句按无法判定计，取反不翻转；为空 / 不为空除外。
    /// </remarks>
    Missing = 1,

    /// <summary>
    /// 暂不可得，稍后能补（如模型绑定前的资源行属性）：子句按待定计。
    /// </summary>
    Deferred = 2,
}
