// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;

namespace XiHan.Framework.Traffic.Tests.Fakes;

/// <summary>
/// 可编排返回值与故障的灰度规则仓储替身
/// </summary>
/// <remarks>
/// 与 InMemoryGrayRuleRepository 不同，本替身不做 IsEnabled 过滤，
/// 目的是让引擎测试能精确控制「引擎拿到的规则集合」，把仓储过滤语义留给仓储自己的测试。
/// </remarks>
public sealed class FakeGrayRuleRepository : IGrayRuleRepository
{
    private readonly List<IGrayRule> _rules;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="rules">GetEnabledRulesAsync 将原样返回的规则</param>
    public FakeGrayRuleRepository(params IGrayRule[] rules)
    {
        _rules = new List<IGrayRule>(rules);
    }

    /// <summary>
    /// 非空时 GetEnabledRulesAsync 以该异常失败，用于模拟仓储不可用
    /// </summary>
    public Exception? GetEnabledRulesException { get; set; }

    /// <summary>
    /// RefreshAsync 被调用的次数
    /// </summary>
    public int RefreshCount { get; private set; }

    /// <summary>
    /// 最近一次 GetEnabledRulesAsync 收到的取消令牌
    /// </summary>
    public CancellationToken LastToken { get; private set; }

    /// <summary>
    /// 获取所有启用的灰度规则
    /// </summary>
    public Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        LastToken = cancellationToken;

        return GetEnabledRulesException is not null
            ? Task.FromException<List<IGrayRule>>(GetEnabledRulesException)
            : Task.FromResult(new List<IGrayRule>(_rules));
    }

    /// <summary>
    /// 根据规则ID获取规则
    /// </summary>
    public Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IGrayRule?>(_rules.FirstOrDefault(rule => rule.RuleId == ruleId));
    }

    /// <summary>
    /// 刷新规则缓存
    /// </summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        RefreshCount++;
        return Task.CompletedTask;
    }
}
