// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Enums;

namespace XiHan.Framework.Traffic.GrayRouting.Abstractions;

/// <summary>
/// 灰度规则接口
/// </summary>
/// <remarks>
/// 定义灰度规则的基本结构，但不参与规则的执行逻辑
/// </remarks>
public interface IGrayRule
{
    /// <summary>
    /// 规则唯一标识
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// 规则名称
    /// </summary>
    string RuleName { get; }

    /// <summary>
    /// 规则类型
    /// </summary>
    GrayRuleType RuleType { get; }

    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 优先级(数字越小优先级越高)
    /// </summary>
    int Priority { get; }
}
