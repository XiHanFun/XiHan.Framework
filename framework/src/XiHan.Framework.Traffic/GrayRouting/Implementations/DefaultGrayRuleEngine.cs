#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultGrayRuleEngine
// Guid:1c2d3e4f-5a6b-7c8d-9e0f-1a2b3c4d5e6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.GrayRouting.Implementations;

/// <summary>
/// 默认灰度规则引擎
/// </summary>
/// <remarks>
/// 负责执行灰度规则的匹配和决策
/// </remarks>
public class DefaultGrayRuleEngine : IGrayRuleEngine
{
    private readonly IGrayRuleRepository _ruleRepository;
    private readonly IEnumerable<IGrayMatcher> _matchers;
    private readonly ILogger<DefaultGrayRuleEngine> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultGrayRuleEngine(
        IGrayRuleRepository ruleRepository,
        IEnumerable<IGrayMatcher> matchers,
        ILogger<DefaultGrayRuleEngine> logger)
    {
        _ruleRepository = ruleRepository;
        _matchers = matchers;
        _logger = logger;
    }

    /// <summary>
    /// 执行灰度决策
    /// </summary>
    public async Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取所有启用的规则
            var rules = await _ruleRepository.GetEnabledRulesAsync(cancellationToken);

            if (rules.Count == 0)
            {
                return GrayDecision.NotGray("没有启用的灰度规则");
            }

            // 按优先级排序
            var sortedRules = rules.OrderBy(r => r.Priority).ToList();

            // 遍历规则进行匹配
            foreach (var rule in sortedRules)
            {
                // 检查规则有效期
                if (!IsRuleEffective(rule))
                {
                    continue;
                }

                // 找到对应的匹配器
                var matcher = _matchers.FirstOrDefault(m => m.RuleType == rule.RuleType);
                if (matcher == null)
                {
                    _logger.LogWarning("找不到规则类型 {RuleType} 的匹配器", rule.RuleType);
                    continue;
                }

                // 执行匹配
                var isMatch = await matcher.IsMatchAsync(context, rule, cancellationToken);
                if (isMatch)
                {
                    _logger.LogDebug("命中灰度规则: {RuleId} - {RuleName}", rule.RuleId, rule.RuleName);

                    return GrayDecision.Gray(
                        targetVersion: (rule as GrayRule)?.TargetVersion ?? "gray",
                        matchedRuleId: rule.RuleId,
                        reason: $"命中规则: {rule.RuleName}");
                }
            }

            return GrayDecision.NotGray("未命中任何灰度规则");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行灰度决策时发生异常");
            return GrayDecision.NotGray($"决策异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查规则是否在有效期内
    /// </summary>
    private bool IsRuleEffective(IGrayRule rule)
    {
        var now = DateTime.UtcNow;

        if (rule is GrayRule grayRule)
        {
            if (grayRule.EffectiveTime.HasValue && now < grayRule.EffectiveTime.Value)
            {
                return false;
            }

            if (grayRule.ExpiryTime.HasValue && now > grayRule.ExpiryTime.Value)
            {
                return false;
            }
        }

        return true;
    }
}
