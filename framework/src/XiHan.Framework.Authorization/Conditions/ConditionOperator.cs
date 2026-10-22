// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 条件运算符
/// </summary>
/// <remarks>
/// 整数值是持久化契约：应用把它存进库里，取值不得调整。
/// 带 Not 的运算符是对应运算符的逻辑否定；子句上的取反再作用于运算结果，两者叠加时按字面计算。
/// </remarks>
public enum ConditionOperator
{
    /// <summary>
    /// 等于（多值属性为任一元素等于）
    /// </summary>
    [Description("等于")]
    Equals = 0,

    /// <summary>
    /// 不等于（等于的否定）
    /// </summary>
    [Description("不等于")]
    NotEquals = 1,

    /// <summary>
    /// 大于
    /// </summary>
    [Description("大于")]
    GreaterThan = 2,

    /// <summary>
    /// 大于等于
    /// </summary>
    [Description("大于等于")]
    GreaterThanOrEquals = 3,

    /// <summary>
    /// 小于
    /// </summary>
    [Description("小于")]
    LessThan = 4,

    /// <summary>
    /// 小于等于
    /// </summary>
    [Description("小于等于")]
    LessThanOrEquals = 5,

    /// <summary>
    /// 包含（单值字符串为子串判断，多值属性为列表含该值）
    /// </summary>
    [Description("包含")]
    Contains = 6,

    /// <summary>
    /// 不包含（包含的否定）
    /// </summary>
    [Description("不包含")]
    NotContains = 7,

    /// <summary>
    /// 在集合中（IP 按 CIDR 包含）
    /// </summary>
    [Description("在集合中")]
    In = 8,

    /// <summary>
    /// 不在集合中（在集合中的否定）
    /// </summary>
    [Description("不在集合中")]
    NotIn = 9,

    /// <summary>
    /// 在闭区间内（时刻允许跨零点）
    /// </summary>
    [Description("在范围内")]
    Between = 10,

    /// <summary>
    /// 以…开头
    /// </summary>
    [Description("以…开头")]
    StartsWith = 11,

    /// <summary>
    /// 以…结尾
    /// </summary>
    [Description("以…结尾")]
    EndsWith = 12,

    /// <summary>
    /// 为空（取不到、空串、空列表）
    /// </summary>
    [Description("为空")]
    IsNull = 13,

    /// <summary>
    /// 不为空
    /// </summary>
    [Description("不为空")]
    IsNotNull = 14
}
