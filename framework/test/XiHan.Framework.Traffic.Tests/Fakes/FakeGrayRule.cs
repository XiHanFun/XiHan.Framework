// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 只实现 <see cref="IGrayRule"/> 接口、不继承 GrayRule 的最小规则替身
/// </summary>
/// <remarks>
/// 用于验证匹配器与引擎在遇到「非 GrayRule 的 IGrayRule 实现」时的降级分支：
/// 匹配器一律不命中，引擎的目标版本回退为 gray。
/// </remarks>
public sealed class FakeGrayRule : IGrayRule
{
    /// <summary>
    /// 规则唯一标识
    /// </summary>
    public string RuleId { get; set; } = "fake-rule";

    /// <summary>
    /// 规则名称
    /// </summary>
    public string RuleName { get; set; } = "假规则";

    /// <summary>
    /// 规则类型
    /// </summary>
    public GrayRuleType RuleType { get; set; } = GrayRuleType.Custom;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; set; }
}
