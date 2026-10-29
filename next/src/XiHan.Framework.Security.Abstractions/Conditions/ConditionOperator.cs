// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件运算符。
/// </summary>
/// <remarks>
/// 整数值是持久化契约：应用把它存进库里，取值不得调整。带 Not 的运算符是对应运算符的逻辑否定；子句上的取反再作用于运算结果，两者叠加时按字面计算。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum ConditionOperator
{
    /// <summary>
    /// 等于（多值属性为任一元素等于）。
    /// </summary>
    Equals = 0,

    /// <summary>
    /// 不等于（等于的否定）。
    /// </summary>
    NotEquals = 1,

    /// <summary>
    /// 大于。
    /// </summary>
    GreaterThan = 2,

    /// <summary>
    /// 大于等于。
    /// </summary>
    GreaterThanOrEquals = 3,

    /// <summary>
    /// 小于。
    /// </summary>
    LessThan = 4,

    /// <summary>
    /// 小于等于。
    /// </summary>
    LessThanOrEquals = 5,

    /// <summary>
    /// 包含（单值字符串为子串判断，多值属性为列表含该值）。
    /// </summary>
    Contains = 6,

    /// <summary>
    /// 不包含（包含的否定）。
    /// </summary>
    NotContains = 7,

    /// <summary>
    /// 在集合中（IP 按 CIDR 网段包含）。
    /// </summary>
    In = 8,

    /// <summary>
    /// 不在集合中（在集合中的否定）。
    /// </summary>
    NotIn = 9,

    /// <summary>
    /// 在闭区间内（时刻允许下限大于上限，表示跨零点）。
    /// </summary>
    Between = 10,

    /// <summary>
    /// 以条件值开头。
    /// </summary>
    StartsWith = 11,

    /// <summary>
    /// 以条件值结尾。
    /// </summary>
    EndsWith = 12,

    /// <summary>
    /// 为空（取不到、空串、空列表）。
    /// </summary>
    IsNull = 13,

    /// <summary>
    /// 不为空。
    /// </summary>
    IsNotNull = 14,
}
