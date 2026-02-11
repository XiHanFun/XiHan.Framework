#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryGrayRuleRepository
// Guid:2d3e4f5a-6b7c-8d9e-0f1a-2b3c4d5e6f7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;

namespace XiHan.Framework.Traffic.GrayRouting.Implementations;

/// <summary>
/// 内存灰度规则仓储
/// </summary>
/// <remarks>
/// 仅用于演示和测试，生产环境应使用数据库或配置中心
/// </remarks>
public class InMemoryGrayRuleRepository : IGrayRuleRepository
{
    private readonly ConcurrentDictionary<string, IGrayRule> _rules = new();

    /// <summary>
    /// 获取所有启用的灰度规则
    /// </summary>
    public Task<List<IGrayRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default)
    {
        var enabledRules = _rules.Values
            .Where(r => r.IsEnabled)
            .ToList();

        return Task.FromResult(enabledRules);
    }

    /// <summary>
    /// 根据规则ID获取规则
    /// </summary>
    public Task<IGrayRule?> GetRuleByIdAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(ruleId, out var rule);
        return Task.FromResult(rule);
    }

    /// <summary>
    /// 刷新规则缓存
    /// </summary>
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // 内存实现无需刷新
        return Task.CompletedTask;
    }

    /// <summary>
    /// 添加规则（仅用于测试）
    /// </summary>
    public void AddRule(IGrayRule rule)
    {
        _rules[rule.RuleId] = rule;
    }

    /// <summary>
    /// 移除规则（仅用于测试）
    /// </summary>
    public void RemoveRule(string ruleId)
    {
        _rules.TryRemove(ruleId, out _);
    }

    /// <summary>
    /// 清空所有规则（仅用于测试）
    /// </summary>
    public void Clear()
    {
        _rules.Clear();
    }
}
