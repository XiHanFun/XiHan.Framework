// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 可编排结果并记录调用轨迹的灰度匹配器替身
/// </summary>
/// <remarks>
/// 记录被评估过的规则标识与收到的取消令牌，用于断言引擎的优先级顺序、首命中短路与令牌透传。
/// </remarks>
public sealed class FakeGrayMatcher : IGrayMatcher
{
    private readonly Func<GrayContext, IGrayRule, bool> _predicate;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="ruleType">该匹配器声明处理的规则类型</param>
    /// <param name="predicate">匹配判定委托，可直接抛异常以模拟匹配器故障</param>
    public FakeGrayMatcher(GrayRuleType ruleType, Func<GrayContext, IGrayRule, bool> predicate)
    {
        RuleType = ruleType;
        _predicate = predicate;
    }

    /// <summary>
    /// 匹配规则类型
    /// </summary>
    public GrayRuleType RuleType { get; }

    /// <summary>
    /// 按调用先后记录的规则标识
    /// </summary>
    public List<string> InvokedRuleIds { get; } = [];

    /// <summary>
    /// 按调用先后记录的取消令牌
    /// </summary>
    public List<CancellationToken> ReceivedTokens { get; } = [];

    /// <summary>
    /// 判断是否命中灰度规则
    /// </summary>
    public bool IsMatch(GrayContext context, IGrayRule rule)
    {
        InvokedRuleIds.Add(rule.RuleId);
        return _predicate(context, rule);
    }

    /// <summary>
    /// 异步判断是否命中灰度规则
    /// </summary>
    public Task<bool> IsMatchAsync(GrayContext context, IGrayRule rule, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(IsMatch(context, rule));
    }
}
