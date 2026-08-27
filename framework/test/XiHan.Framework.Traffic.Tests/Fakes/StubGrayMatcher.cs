// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 带公共无参构造函数的自定义匹配器替身，专供依赖注入装配测试使用
/// </summary>
/// <remarks>
/// 用于验证 AddGrayMatcher&lt;TMatcher&gt; 能把外部匹配器追加进 IGrayMatcher 集合而不影响内置匹配器。
/// </remarks>
public sealed class StubGrayMatcher : IGrayMatcher
{
    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType => GrayRuleType.Custom;

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        return false;
    }

    /// <summary>
    /// 异步判断是否命中灰度规则
    /// </summary>
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
