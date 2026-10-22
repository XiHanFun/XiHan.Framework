// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 属性取值状态
/// </summary>
public enum ConditionAttributeValueState
{
    /// <summary>
    /// 有值（多值属性可以是空列表）
    /// </summary>
    Present = 0,

    /// <summary>
    /// 取不到（子句按「无法判定」计，取反不翻转；为空 / 不为空除外）
    /// </summary>
    Missing = 1,

    /// <summary>
    /// 暂不可得，稍后能补（如模型绑定前的资源行属性），子句按「待定」计
    /// </summary>
    Deferred = 2
}
